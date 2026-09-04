import { useCallback } from "react";
import { tr, type TranslationKey } from "./tr";
import { en } from "./en";
import { useLanguageStore, type Language } from "./store";

const DICTIONARIES: Record<Language, Record<TranslationKey, string>> = { tr, en };

export type TranslationParams = Record<string, string | number>;
export type Translate = (key: TranslationKey, params?: TranslationParams) => string;

function fill(template: string, params?: TranslationParams): string {
  if (!params) return template;

  return template.replace(/\{(\w+)\}/g, (token, name: string) =>
    name in params ? String(params[name]) : token
  );
}

export function translateIn(
  language: Language,
  key: TranslationKey,
  params?: TranslationParams
): string {
  return fill(DICTIONARIES[language][key], params);
}

export function translate(key: TranslationKey, params?: TranslationParams): string {
  return translateIn(useLanguageStore.getState().language, key, params);
}

export function useT(): Translate {
  const language = useLanguageStore((state) => state.language);

  return useCallback(
    (key: TranslationKey, params?: TranslationParams) => translateIn(language, key, params),
    [language]
  );
}
