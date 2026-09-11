import "@testing-library/jest-dom/vitest";
import { afterEach } from "vitest";

afterEach(() => {
  for (const cookie of document.cookie.split(";")) {
    const name = cookie.split("=")[0]?.trim();
    if (name) document.cookie = `${name}=; max-age=0; path=/`;
  }
});
