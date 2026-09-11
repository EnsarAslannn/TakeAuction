import axios from "axios";
import type { AxiosAdapter, AxiosRequestConfig, AxiosResponse } from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError, http, toApiError, waitForRotatedSession } from "./client";

const CSRF_COOKIE = "takeauction_csrf";
const CSRF_HEADER = "x-csrf-token";

function setCsrfCookie(value: string) {
  document.cookie = `${CSRF_COOKIE}=${value}; path=/`;
}

function ok(config: AxiosRequestConfig, data: unknown = {}): AxiosResponse {
  return {
    data,
    status: 200,
    statusText: "OK",
    headers: {},
    config: config as AxiosResponse["config"],
  };
}

function headerOf(config: AxiosRequestConfig, name: string): string | undefined {
  const headers = config.headers as { get?: (key: string) => unknown } | undefined;
  const value = headers?.get?.(name) ?? (headers as Record<string, unknown> | undefined)?.[name];

  return value === undefined || value === null ? undefined : String(value);
}

describe("the CSRF interceptor", () => {
  beforeEach(() => setCsrfCookie("token-from-the-cookie"));

  it("sends the double-submit header on a mutating call", async () => {
    const seen: AxiosRequestConfig[] = [];
    http.defaults.adapter = ((config: AxiosRequestConfig) => {
      seen.push(config);
      return Promise.resolve(ok(config));
    }) as AxiosAdapter;

    await http.post("/auctions/x/bids", { amount: 10 });

    expect(headerOf(seen[0], CSRF_HEADER)).toBe("token-from-the-cookie");
  });

  it("leaves a read alone, because the API only guards unsafe methods", async () => {
    const seen: AxiosRequestConfig[] = [];
    http.defaults.adapter = ((config: AxiosRequestConfig) => {
      seen.push(config);
      return Promise.resolve(ok(config));
    }) as AxiosAdapter;

    await http.get("/auctions");

    expect(headerOf(seen[0], CSRF_HEADER)).toBeUndefined();
  });

  it("sends nothing when there is no session cookie to echo", async () => {
    document.cookie = `${CSRF_COOKIE}=; max-age=0; path=/`;

    const seen: AxiosRequestConfig[] = [];
    http.defaults.adapter = ((config: AxiosRequestConfig) => {
      seen.push(config);
      return Promise.resolve(ok(config));
    }) as AxiosAdapter;

    await http.post("/auctions/x/bids", { amount: 10 });

    expect(headerOf(seen[0], CSRF_HEADER)).toBeUndefined();
  });
});

describe("waitForRotatedSession", () => {
  // A 409 means another tab claimed the refresh first. Its reply carries the new cookies,
  // and the readable CSRF cookie changing is the signal that they have landed.
  it("returns as soon as the winner's cookies land", async () => {
    setCsrfCookie("stale-token");

    const settled = waitForRotatedSession("stale-token", { timeoutMs: 1000, pollMs: 5 });
    setTimeout(() => setCsrfCookie("rotated-token"), 20);

    await expect(settled).resolves.toBe(true);
  });

  it("does not wait at all when the rotation already landed", async () => {
    setCsrfCookie("rotated-token");

    await expect(
      waitForRotatedSession("stale-token", { timeoutMs: 1000, pollMs: 5 })
    ).resolves.toBe(true);
  });

  it("gives up when no rotation ever arrives, rather than hanging the call", async () => {
    setCsrfCookie("stale-token");

    await expect(
      waitForRotatedSession("stale-token", { timeoutMs: 40, pollMs: 5 })
    ).resolves.toBe(false);
  });
});

describe("toApiError", () => {
  it("keeps the problem document the API sent", () => {
    const error = toApiError({
      isAxiosError: true,
      response: { status: 409, data: { title: "Bid too low", detail: "Raise it." } },
    });

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(409);
    expect(error.message).toBe("Raise it.");
  });

  it("reports a network failure as unreachable rather than a status", () => {
    const error = toApiError({ isAxiosError: true, response: undefined });

    expect(error.status).toBe(0);
  });

  it("exposes field errors for a validation problem", () => {
    const error = toApiError({
      isAxiosError: true,
      response: { status: 400, data: { errors: { Amount: ["must be higher"] } } },
    });

    expect(error.fieldErrors).toEqual({ Amount: ["must be higher"] });
  });

  it("passes an ApiError straight through", () => {
    const original = new ApiError(500, { title: "Boom" });

    expect(toApiError(original)).toBe(original);
  });
});

describe("the session retry", () => {
  it("refreshes once on a 401 and replays the original call", async () => {
    setCsrfCookie("token-from-the-cookie");

    const refresh = vi.fn();
    let bidAttempts = 0;

    const adapter = ((config: AxiosRequestConfig) => {
      const url = config.url ?? "";

      if (url.includes("/auth/refresh")) {
        refresh();
        return Promise.resolve(ok(config));
      }

      bidAttempts += 1;

      if (bidAttempts === 1) {
        return Promise.reject(
          Object.assign(new Error("unauthorised"), {
            isAxiosError: true,
            config,
            response: { status: 401, data: {}, config },
          })
        );
      }

      return Promise.resolve(ok(config, { replayed: true }));
    }) as AxiosAdapter;

    // The refresh deliberately goes out on the bare axios instance so it cannot recurse
    // through this interceptor, so the stub has to stand in for both.
    http.defaults.adapter = adapter;
    axios.defaults.adapter = adapter;

    const response = await http.get("/auctions/mine");

    expect(refresh).toHaveBeenCalledTimes(1);
    expect(bidAttempts).toBe(2);
    expect(response.data).toEqual({ replayed: true });
  });

  it("never tries to refresh a refresh", async () => {
    setCsrfCookie("token-from-the-cookie");

    let attempts = 0;

    http.defaults.adapter = ((config: AxiosRequestConfig) => {
      attempts += 1;

      return Promise.reject(
        Object.assign(new Error("unauthorised"), {
          isAxiosError: true,
          config,
          response: { status: 401, data: {}, config },
        })
      );
    }) as AxiosAdapter;

    await expect(http.post("/auth/refresh")).rejects.toBeInstanceOf(ApiError);
    expect(attempts).toBe(1);
  });
});
