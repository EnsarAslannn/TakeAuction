import { beforeEach, describe, expect, it } from "vitest";
import {
  CHAT_STORAGE_KEY,
  createConversation,
  loadChatState,
  saveChatState,
  type ChatState,
} from "./chatStorage";

describe("chatStorage", () => {
  beforeEach(() => window.localStorage.clear());

  it("restores multiple conversations and their messages after a reload", () => {
    const first = createConversation("tr", "one");
    first.title = "Teklifler";
    first.messages.push({ id: "m1", role: "user", content: "Teklif nasıl verilir?" });
    const second = createConversation("en", "two");
    const state: ChatState = { activeConversationId: second.id, conversations: [first, second] };

    saveChatState(state);

    expect(loadChatState()).toEqual(state);
  });

  it("ignores malformed browser data instead of breaking the site", () => {
    window.localStorage.setItem(CHAT_STORAGE_KEY, "not-json");

    expect(loadChatState()).toEqual({ activeConversationId: null, conversations: [] });
  });

  it("creates an empty conversation with the selected language", () => {
    expect(createConversation("en", "conversation-id")).toMatchObject({
      id: "conversation-id",
      language: "en",
      messages: [],
    });
  });
});
