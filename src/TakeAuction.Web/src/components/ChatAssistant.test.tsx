import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { postChat } from "@/api/chat";
import { CHAT_STORAGE_KEY, type ChatState } from "@/chat/chatStorage";
import { useLanguageStore } from "@/i18n";
import { useAuthStore } from "@/store/authStore";
import { ChatAssistant } from "./ChatAssistant";

vi.mock("@/api/chat", () => ({ postChat: vi.fn() }));

const renderAssistant = () => render(<MemoryRouter><ChatAssistant /></MemoryRouter>);

describe("ChatAssistant", () => {
  beforeEach(() => {
    window.localStorage.clear();
    useLanguageStore.setState({ language: "tr" });
    useAuthStore.setState({ user: null, status: "ready", error: null });
    vi.mocked(postChat).mockReset();
  });

  it("opens accessibly, moves focus into the panel, and returns it on Escape", async () => {
    renderAssistant();
    const launcher = screen.getByRole("button", { name: "TakeAuction asistanını aç" });
    expect(launcher).toHaveTextContent("Asistan");

    fireEvent.click(launcher);

    expect(screen.getByRole("dialog", { name: "TakeAuction asistanı" })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("textbox", { name: "Sorunuz" })).toHaveFocus());

    fireEvent.keyDown(document, { key: "Escape" });

    expect(screen.queryByRole("dialog", { name: "TakeAuction asistanı" })).not.toBeInTheDocument();
    expect(launcher).toHaveFocus();
  });

  it("sends recent history and renders sources plus at most three server suggestions", async () => {
    vi.mocked(postChat)
      .mockResolvedValueOnce({
        answer: "Teklif vermek için oturum açın.",
        sources: [{ title: "Nasıl çalışır", url: "/#how-it-works" }],
        suggestions: ["Limit nedir?", "Canlı güncellemeler nasıl gelir?", "Kim kazanır?"],
        usedAi: false,
      })
      .mockResolvedValueOnce({
        answer: "Limitiniz diğer kullanıcılara gösterilmez.",
        sources: [{ title: "Nasıl çalışır", url: "/#how-it-works" }],
        suggestions: ["Minimum artış nedir?"],
        usedAi: false,
      });
    renderAssistant();
    fireEvent.click(screen.getByRole("button", { name: "TakeAuction asistanını aç" }));

    const input = screen.getByRole("textbox", { name: "Sorunuz" });
    fireEvent.change(input, { target: { value: "Teklif nasıl verilir?" } });
    fireEvent.click(screen.getByRole("button", { name: "Gönder" }));
    await screen.findByText("Teklif vermek için oturum açın.");

    fireEvent.change(input, { target: { value: "Limitim görünür mü?" } });
    fireEvent.click(screen.getByRole("button", { name: "Gönder" }));
    await screen.findByText("Limitiniz diğer kullanıcılara gösterilmez.");

    expect(postChat).toHaveBeenLastCalledWith({
      message: "Limitim görünür mü?",
      language: "tr",
      history: [
        { role: "user", content: "Teklif nasıl verilir?" },
        { role: "assistant", content: "Teklif vermek için oturum açın." },
      ],
    });
    expect(screen.getAllByRole("link", { name: "Nasıl çalışır" })).toHaveLength(2);
    expect(screen.getAllByRole("button", { name: /^(Limit nedir\?|Canlı güncellemeler nasıl gelir\?|Kim kazanır\?|Minimum artış nedir\?)$/ })).toHaveLength(1);
  });

  it("restores old chats, creates another chat, and clears all only after confirmation", async () => {
    const stored: ChatState = {
      activeConversationId: "old",
      conversations: [{
        id: "old",
        title: "Eski konuşma",
        language: "tr",
        createdAt: "2026-09-20T10:00:00.000Z",
        updatedAt: "2026-09-20T10:00:00.000Z",
        messages: [{ id: "m1", role: "user", content: "Eski sorum" }],
      }],
    };
    window.localStorage.setItem(CHAT_STORAGE_KEY, JSON.stringify(stored));
    renderAssistant();
    fireEvent.click(screen.getByRole("button", { name: "TakeAuction asistanını aç" }));

    fireEvent.click(screen.getByRole("button", { name: "Konuşmalar" }));
    expect(screen.getByRole("button", { name: /Eski konuşma/ })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Yeni sohbet" }));
    expect(screen.getByText("Size nasıl yardımcı olabilirim?")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Konuşmalar" }));
    fireEvent.click(screen.getByRole("button", { name: "Tüm konuşmaları temizle" }));
    let confirmation = screen.getByRole("alertdialog", { name: "Tüm konuşmalar silinsin mi?" });
    await waitFor(() => expect(within(confirmation).getByRole("button", { name: "Evet, temizle" })).toHaveFocus());
    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.queryByRole("alertdialog", { name: "Tüm konuşmalar silinsin mi?" })).not.toBeInTheDocument();
    expect(screen.getByRole("dialog", { name: "TakeAuction asistanı" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Tüm konuşmaları temizle" }));
    confirmation = screen.getByRole("alertdialog", { name: "Tüm konuşmalar silinsin mi?" });
    fireEvent.click(within(confirmation).getByRole("button", { name: "Evet, temizle" }));

    expect(screen.queryByText("Eski konuşma")).not.toBeInTheDocument();
    const saved = JSON.parse(window.localStorage.getItem(CHAT_STORAGE_KEY) ?? "{}") as ChatState;
    expect(saved.conversations).toHaveLength(1);
    expect(saved.conversations[0].messages).toEqual([]);
  });

  it("shows private shortcuts only to signed-in users", () => {
    const { unmount } = renderAssistant();
    fireEvent.click(screen.getByRole("button", { name: "TakeAuction asistanını aç" }));
    expect(screen.queryByRole("link", { name: "Takip listem" })).not.toBeInTheDocument();
    unmount();

    useAuthStore.setState({
      user: {
        id: "user-1",
        email: "user@example.com",
        displayName: "Ada",
        role: "Bidder",
        createdAtUtc: "2026-09-20T10:00:00.000Z",
        lastLoginAtUtc: null,
      },
      status: "ready",
    });
    renderAssistant();
    fireEvent.click(screen.getByRole("button", { name: "TakeAuction asistanını aç" }));

    expect(screen.getByRole("link", { name: "Takip listem" })).toHaveAttribute("href", "/watchlist");
  });
});
