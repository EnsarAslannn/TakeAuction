import type { Language } from "@/i18n";
import type { ChatSource } from "@/api/chat";

export const CHAT_STORAGE_KEY = "takeauction_chat_conversations_v1";

export interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  sources?: ChatSource[];
  suggestions?: string[];
}

export interface ChatConversation {
  id: string;
  title: string;
  language: Language;
  createdAt: string;
  updatedAt: string;
  messages: ChatMessage[];
}

export interface ChatState {
  activeConversationId: string | null;
  conversations: ChatConversation[];
}

const emptyState = (): ChatState => ({ activeConversationId: null, conversations: [] });

function id(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

export function createConversation(language: Language, conversationId = id()): ChatConversation {
  const timestamp = new Date().toISOString();
  return {
    id: conversationId,
    title: language === "tr" ? "Yeni sohbet" : "New chat",
    language,
    createdAt: timestamp,
    updatedAt: timestamp,
    messages: [],
  };
}

function isMessage(value: unknown): value is ChatMessage {
  if (!value || typeof value !== "object") return false;
  const message = value as Partial<ChatMessage>;
  return typeof message.id === "string" &&
    (message.role === "user" || message.role === "assistant") &&
    typeof message.content === "string";
}

function isConversation(value: unknown): value is ChatConversation {
  if (!value || typeof value !== "object") return false;
  const conversation = value as Partial<ChatConversation>;
  return typeof conversation.id === "string" &&
    typeof conversation.title === "string" &&
    (conversation.language === "tr" || conversation.language === "en") &&
    typeof conversation.createdAt === "string" &&
    typeof conversation.updatedAt === "string" &&
    Array.isArray(conversation.messages) && conversation.messages.every(isMessage);
}

export function loadChatState(): ChatState {
  try {
    const raw = window.localStorage.getItem(CHAT_STORAGE_KEY);
    if (!raw) return emptyState();

    const parsed = JSON.parse(raw) as Partial<ChatState>;
    if (!Array.isArray(parsed.conversations) || !parsed.conversations.every(isConversation)) {
      return emptyState();
    }

    const activeConversationId = typeof parsed.activeConversationId === "string" &&
      parsed.conversations.some((conversation) => conversation.id === parsed.activeConversationId)
      ? parsed.activeConversationId
      : parsed.conversations[0]?.id ?? null;

    return { activeConversationId, conversations: parsed.conversations };
  } catch {
    return emptyState();
  }
}

export function saveChatState(state: ChatState): void {
  try {
    window.localStorage.setItem(CHAT_STORAGE_KEY, JSON.stringify(state));
  } catch {
    void 0;
  }
}
