import type { Language } from "@/i18n";
import { http } from "./client";

export interface ChatHistoryItem {
  role: "user" | "assistant";
  content: string;
}

export interface ChatRequest {
  message: string;
  language: Language;
  history: ChatHistoryItem[];
}

export interface ChatSource {
  title: string;
  url: string;
}

export interface ChatResponse {
  answer: string;
  sources: ChatSource[];
  suggestions: string[];
  usedAi: boolean;
}

export async function postChat(request: ChatRequest): Promise<ChatResponse> {
  const response = await http.post<ChatResponse>("/chat", request);
  return response.data;
}
