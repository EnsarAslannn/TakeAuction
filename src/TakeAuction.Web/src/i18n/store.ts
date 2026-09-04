import { create } from "zustand";

export type Language = "tr" | "en";

export const DEFAULT_LANGUAGE: Language = "tr";

const STORAGE_KEY = "takeauction_lang";

function readStoredLanguage(): Language {
  try {
    return window.localStorage.getItem(STORAGE_KEY) === "en" ? "en" : DEFAULT_LANGUAGE;
  } catch {
    return DEFAULT_LANGUAGE;
  }
}

interface LanguageState {
  language: Language;
  setLanguage: (language: Language) => void;
}

export const useLanguageStore = create<LanguageState>((set) => ({
  language: readStoredLanguage(),
  setLanguage: (language) => {
    try {
      window.localStorage.setItem(STORAGE_KEY, language);
    } catch {
      void 0;
    }

    set({ language });
  },
}));
