import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { postChat } from "@/api/chat";
import {
  createConversation,
  loadChatState,
  saveChatState,
  type ChatConversation,
  type ChatMessage,
  type ChatState,
} from "@/chat/chatStorage";
import { useLanguageStore, useT } from "@/i18n";
import { canSell, useAuthStore } from "@/store/authStore";

const HISTORY_LIMIT = 8;

function messageId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function initialState(language: "tr" | "en"): ChatState {
  const stored = loadChatState();
  if (stored.conversations.length > 0) return stored;

  const conversation = createConversation(language);
  return { activeConversationId: conversation.id, conversations: [conversation] };
}

function replaceConversation(
  state: ChatState,
  conversationId: string,
  change: (conversation: ChatConversation) => ChatConversation
): ChatState {
  return {
    ...state,
    conversations: state.conversations.map((conversation) =>
      conversation.id === conversationId ? change(conversation) : conversation
    ),
  };
}

export function ChatAssistant() {
  const language = useLanguageStore((value) => value.language);
  const user = useAuthStore((value) => value.user);
  const t = useT();
  const [open, setOpen] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const [confirmClear, setConfirmClear] = useState(false);
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);
  const [state, setState] = useState<ChatState>(() => initialState(language));
  const launcherRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const messageEndRef = useRef<HTMLDivElement>(null);
  const clearButtonRef = useRef<HTMLButtonElement>(null);
  const confirmButtonRef = useRef<HTMLButtonElement>(null);

  const activeConversation = state.conversations.find(
    (conversation) => conversation.id === state.activeConversationId
  ) ?? state.conversations[0];

  const latestAssistant = [...(activeConversation?.messages ?? [])]
    .reverse()
    .find((message) => message.role === "assistant");
  const isEmptyConversation = (activeConversation?.messages.length ?? 0) === 0;
  const useExpandedPanel = showHistory || !isEmptyConversation;

  useEffect(() => saveChatState(state), [state]);

  useEffect(() => {
    if (!open) return;
    window.setTimeout(() => inputRef.current?.focus(), 0);
  }, [open, showHistory]);

  useEffect(() => {
    if (open && typeof messageEndRef.current?.scrollIntoView === "function") {
      messageEndRef.current.scrollIntoView({ block: "end" });
    }
  }, [activeConversation?.messages.length, open]);

  useEffect(() => {
    if (!open) return;

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      if (confirmClear) {
        setConfirmClear(false);
        window.setTimeout(() => clearButtonRef.current?.focus(), 0);
        return;
      }
      setConfirmClear(false);
      launcherRef.current?.focus();
      setOpen(false);
    };

    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [confirmClear, open]);

  useEffect(() => {
    if (confirmClear) {
      window.setTimeout(() => confirmButtonRef.current?.focus(), 0);
    }
  }, [confirmClear]);

  const close = () => {
    setConfirmClear(false);
    launcherRef.current?.focus();
    setOpen(false);
  };

  const newConversation = () => {
    const conversation = createConversation(language);
    setState((previous) => ({
      activeConversationId: conversation.id,
      conversations: [conversation, ...previous.conversations],
    }));
    setShowHistory(false);
    setInput("");
  };

  const clearAll = () => {
    const conversation = createConversation(language);
    setState({ activeConversationId: conversation.id, conversations: [conversation] });
    setConfirmClear(false);
    setShowHistory(false);
    setInput("");
  };

  const sendMessage = async (suggestedMessage?: string) => {
    const message = (suggestedMessage ?? input).trim();
    if (!message || sending || !activeConversation) return;

    const conversationId = activeConversation.id;
    const history = activeConversation.messages.slice(-HISTORY_LIMIT).map(({ role, content }) => ({
      role,
      content,
    }));
    const userMessage: ChatMessage = { id: messageId(), role: "user", content: message };
    const timestamp = new Date().toISOString();

    setInput("");
    setSending(true);
    setState((previous) => replaceConversation(previous, conversationId, (conversation) => ({
      ...conversation,
      title: conversation.messages.length === 0
        ? `${message.slice(0, 42)}${message.length > 42 ? "…" : ""}`
        : conversation.title,
      updatedAt: timestamp,
      messages: [...conversation.messages, userMessage],
    })));

    try {
      const response = await postChat({ message, language, history });
      const assistantMessage: ChatMessage = {
        id: messageId(),
        role: "assistant",
        content: response.answer,
        sources: response.sources,
        suggestions: response.suggestions.slice(0, 3),
      };
      setState((previous) => replaceConversation(previous, conversationId, (conversation) => ({
        ...conversation,
        updatedAt: new Date().toISOString(),
        messages: [...conversation.messages, assistantMessage],
      })));
    } catch {
      setState((previous) => replaceConversation(previous, conversationId, (conversation) => ({
        ...conversation,
        updatedAt: new Date().toISOString(),
        messages: [...conversation.messages, {
          id: messageId(),
          role: "assistant",
          content: t("chat.error"),
        }],
      })));
    } finally {
      setSending(false);
      window.setTimeout(() => inputRef.current?.focus(), 0);
    }
  };

  const onPanelKeyDown = (event: React.KeyboardEvent<HTMLElement>) => {
    if (event.key !== "Tab" || !panelRef.current) return;
    const focusRoot = confirmClear
      ? panelRef.current.querySelector<HTMLElement>('[role="alertdialog"]')
      : panelRef.current;
    const focusable = Array.from(focusRoot?.querySelectorAll<HTMLElement>(
      'button:not([disabled]), a[href], textarea:not([disabled])'
    ) ?? []);
    const first = focusable[0];
    const last = focusable.at(-1);

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first?.focus();
    }
  };

  return (
    <div className="fixed bottom-4 right-4 z-[70] sm:bottom-6 sm:right-6">
      {open && (
        <section
          ref={panelRef}
          role="dialog"
          aria-label={t("chat.title")}
          onKeyDown={onPanelKeyDown}
          className={`fixed inset-x-0 bottom-0 flex max-h-[calc(100dvh-1rem)] flex-col overflow-hidden border border-ink/15 bg-paper-pure shadow-[0_24px_80px_rgba(26,24,21,0.28)] sm:inset-x-auto sm:bottom-24 sm:right-6 sm:max-h-[calc(100dvh-8rem)] sm:w-[26rem] ${
            useExpandedPanel
              ? "h-[min(46rem,calc(100dvh-1rem))] sm:h-[min(42rem,calc(100dvh-8rem))]"
              : ""
          }`}
        >
          <header className="relative shrink-0 border-b border-paper/15 bg-ink px-5 py-4 text-paper">
            <div aria-hidden className="absolute inset-y-0 left-0 w-1 bg-sand" />
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="font-display text-lg font-medium tracking-tight">{t("chat.title")}</p>
                <p className="mt-1 font-mono text-[0.58rem] uppercase tracking-[0.16em] text-paper/50">
                  {t("chat.localKnowledge")}
                </p>
              </div>
              <button
                type="button"
                onClick={close}
                aria-label={t("chat.close")}
                className="grid h-9 w-9 place-items-center rounded-full border border-paper/20 text-xl text-paper transition-colors hover:border-sand hover:text-sand focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sand"
              >
                <span aria-hidden>×</span>
              </button>
            </div>
            <div className="mt-4 flex gap-2">
              <button
                type="button"
                onClick={() => setShowHistory((value) => !value)}
                aria-pressed={showHistory}
                className="rounded-full border border-paper/20 px-3 py-2 font-mono text-[0.6rem] uppercase tracking-[0.14em] transition-colors hover:bg-paper hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sand"
              >
                {t("chat.conversations")}
              </button>
              <button
                type="button"
                onClick={newConversation}
                aria-label={t("chat.new")}
                className="rounded-full bg-sand px-3 py-2 font-mono text-[0.6rem] uppercase tracking-[0.14em] text-ink transition-colors hover:bg-sand-pale focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sand"
              >
                + {t("chat.new")}
              </button>
            </div>
          </header>

          {showHistory ? (
            <div className="flex min-h-0 flex-1 flex-col">
              <div className="min-h-0 flex-1 overflow-y-auto p-4">
                {state.conversations.length === 0 ? (
                  <p className="py-8 text-center text-sm text-stone">{t("chat.emptyHistory")}</p>
                ) : (
                  <div className="space-y-2">
                    {[...state.conversations]
                      .sort((left, right) => right.updatedAt.localeCompare(left.updatedAt))
                      .map((conversation) => (
                        <button
                          type="button"
                          key={conversation.id}
                          onClick={() => {
                            setState((previous) => ({ ...previous, activeConversationId: conversation.id }));
                            setShowHistory(false);
                          }}
                          className={`w-full border p-4 text-left transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sand-deep ${
                            conversation.id === activeConversation?.id
                              ? "border-sand-deep bg-paper-warm/60"
                              : "border-ink/10 hover:border-sand"
                          }`}
                        >
                          <span className="block truncate font-display text-base text-ink">{conversation.title}</span>
                          <span className="mt-1 block font-mono text-[0.58rem] uppercase tracking-[0.12em] text-stone">
                            {new Intl.DateTimeFormat(conversation.language, { dateStyle: "medium" }).format(new Date(conversation.updatedAt))}
                          </span>
                        </button>
                      ))}
                  </div>
                )}
              </div>
              <div className="border-t border-ink/10 p-4">
                <button
                  ref={clearButtonRef}
                  type="button"
                  onClick={() => setConfirmClear(true)}
                  className="w-full py-2 text-center font-mono text-[0.62rem] uppercase tracking-[0.14em] text-stone transition-colors hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sand-deep"
                >
                  {t("chat.clearAll")}
                </button>
              </div>
            </div>
          ) : (
            <>
              <div
                aria-live="polite"
                className={`min-h-0 overflow-y-auto px-4 py-4 ${isEmptyConversation ? "" : "flex-1"}`}
              >
                {isEmptyConversation ? (
                  <div className="flex flex-col py-1">
                    <p className="eyebrow">TakeAuction / guide</p>
                    <h2 className="mt-3 font-display text-3xl font-light tracking-headline text-ink">
                      {t("chat.welcome")}
                    </h2>
                    <p className="mt-3 text-sm leading-relaxed text-ink/65">{t("chat.intro")}</p>
                    <div className="mt-5 grid gap-2">
                      {[t("chat.quickHelp"), t("chat.quickBid"), t("chat.quickBrowse")].map((question) => (
                        <button
                          type="button"
                          key={question}
                          onClick={() => void sendMessage(question)}
                          className="border border-ink/10 bg-paper px-4 py-3 text-left font-sans text-sm text-ink transition-colors hover:border-sand-deep hover:bg-paper-warm/50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sand-deep"
                        >
                          {question} <span aria-hidden className="float-right text-sand-deep">↗</span>
                        </button>
                      ))}
                    </div>
                    {user && (
                      <div className="mt-6 border-t border-ink/10 pt-4">
                        <p className="eyebrow mb-3">{t("chat.private")}</p>
                        <div className="flex flex-wrap gap-2">
                          <Link to="/watchlist" onClick={close} className="btn-ghost px-4! py-2!">
                            {t("chat.watchlist")}
                          </Link>
                          {canSell(user) && (
                            <Link to="/auctions/new" onClick={close} className="btn-ghost px-4! py-2!">
                              {t("chat.createListing")}
                            </Link>
                          )}
                        </div>
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="space-y-5">
                    {activeConversation?.messages.map((message) => (
                      <article
                        key={message.id}
                        className={message.role === "user" ? "ml-8" : "mr-5"}
                        aria-label={message.role === "user" ? t("chat.you") : t("chat.assistant")}
                      >
                        <p className="mb-1.5 font-mono text-[0.56rem] uppercase tracking-[0.15em] text-stone">
                          {message.role === "user" ? t("chat.you") : t("chat.assistant")}
                        </p>
                        <div className={message.role === "user"
                          ? "border-r-2 border-sand bg-paper-warm/70 px-4 py-3 text-sm leading-relaxed text-ink"
                          : "border-l-2 border-ink/20 bg-paper px-4 py-3 text-sm leading-relaxed text-ink/80"
                        }>
                          <p>{message.content}</p>
                          {message.sources && message.sources.length > 0 && (
                            <div className="mt-3 border-t border-ink/10 pt-3">
                              <span className="mr-2 font-mono text-[0.55rem] uppercase tracking-[0.14em] text-stone">
                                {t("chat.source")}:
                              </span>
                              {message.sources.map((source) => (
                                <Link
                                  key={`${message.id}-${source.url}`}
                                  to={source.url}
                                  onClick={close}
                                  className="font-mono text-[0.62rem] uppercase tracking-[0.1em] text-sand-deep underline decoration-sand/50 underline-offset-4 hover:text-ink"
                                >
                                  {source.title}
                                </Link>
                              ))}
                            </div>
                          )}
                        </div>
                      </article>
                    ))}
                    {sending && (
                      <p role="status" className="font-mono text-[0.6rem] uppercase tracking-[0.14em] text-stone">
                        {t("chat.sending")}
                      </p>
                    )}
                    {latestAssistant?.suggestions && !sending && (
                      <div className="flex flex-wrap gap-2 pt-1">
                        {latestAssistant.suggestions.slice(0, 3).map((suggestion) => (
                          <button
                            type="button"
                            key={suggestion}
                            onClick={() => void sendMessage(suggestion)}
                            className="rounded-full border border-ink/15 px-3 py-2 text-left font-sans text-xs text-ink/75 transition-colors hover:border-sand-deep hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sand-deep"
                          >
                            {suggestion}
                          </button>
                        ))}
                      </div>
                    )}
                    <div ref={messageEndRef} />
                  </div>
                )}
              </div>

              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  void sendMessage();
                }}
                className="shrink-0 border-t border-ink/10 bg-paper-pure p-4"
              >
                <label htmlFor="takeauction-chat-input" className="sr-only">{t("chat.questionLabel")}</label>
                <div className="flex items-end gap-3">
                  <textarea
                    ref={inputRef}
                    id="takeauction-chat-input"
                    rows={2}
                    maxLength={1000}
                    value={input}
                    disabled={sending}
                    onChange={(event) => setInput(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" && !event.shiftKey) {
                        event.preventDefault();
                        void sendMessage();
                      }
                    }}
                    placeholder={t("chat.placeholder")}
                    className="min-h-12 flex-1 resize-none border-0 border-b border-ink/20 bg-transparent px-0 py-2 text-sm text-ink outline-none placeholder:text-stone/60 focus:border-sand-deep disabled:opacity-50"
                  />
                  <button
                    type="submit"
                    disabled={sending || input.trim().length === 0}
                    className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-ink text-paper transition-colors hover:bg-sand-deep focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sand-deep disabled:cursor-not-allowed disabled:opacity-35"
                    aria-label={t("chat.send")}
                  >
                    <span aria-hidden>↑</span>
                  </button>
                </div>
              </form>
            </>
          )}

          {confirmClear && (
            <div className="absolute inset-0 grid place-items-center bg-ink/45 p-5 backdrop-blur-sm">
              <div
                role="alertdialog"
                aria-modal="true"
                aria-label={t("chat.clearConfirmTitle")}
                className="w-full border border-ink/15 bg-paper-pure p-6 shadow-2xl"
              >
                <h2 className="font-display text-2xl font-light text-ink">{t("chat.clearConfirmTitle")}</h2>
                <p className="mt-3 text-sm leading-relaxed text-ink/65">{t("chat.clearConfirmBody")}</p>
                <div className="mt-6 flex flex-wrap gap-2">
                  <button ref={confirmButtonRef} type="button" onClick={clearAll} className="btn-primary px-4! py-2.5!">
                    {t("chat.clearConfirm")}
                  </button>
                  <button type="button" onClick={() => setConfirmClear(false)} className="btn-ghost px-4! py-2.5!">
                    {t("chat.cancel")}
                  </button>
                </div>
              </div>
            </div>
          )}
        </section>
      )}

      <button
        ref={launcherRef}
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-label={t("chat.open")}
        aria-expanded={open}
        aria-hidden={open}
        tabIndex={open ? -1 : 0}
        className={`group ml-auto inline-flex h-16 min-w-32 items-center justify-center gap-3 rounded-full border px-4 shadow-[0_14px_44px_rgba(26,24,21,0.34),0_0_0_3px_rgba(192,160,112,0.16)] transition-all duration-300 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-sand-deep ${
          open ? "pointer-events-none scale-90 border-paper/15 bg-ink/0 opacity-0" : "border-sand/60 bg-ink text-paper hover:-translate-y-1 hover:border-sand hover:bg-ink-soft"
        }`}
      >
        <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-sand text-ink transition-transform duration-300 group-hover:rotate-[-4deg] group-hover:scale-105">
          <svg aria-hidden viewBox="0 0 24 24" className="h-5 w-5 fill-none stroke-current" strokeWidth="1.7">
            <path d="M5.5 6.5h13v9h-7l-4.5 3v-3H5.5z" />
            <path d="M9 10h6M9 12.75h4" />
          </svg>
        </span>
        <span className="pr-1 font-display text-base font-medium tracking-tight">
          {t("chat.launcher")}
        </span>
      </button>
    </div>
  );
}
