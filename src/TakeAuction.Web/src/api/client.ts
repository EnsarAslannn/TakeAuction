import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios";
import type { ProblemDetails } from "./types";

export const API_BASE = import.meta.env.VITE_API_BASE ?? "/api/v1";
export const HUB_URL = import.meta.env.VITE_HUB_URL ?? "/hubs/auctions";

const CSRF_COOKIE = "takeauction_csrf";
const CSRF_HEADER = "X-CSRF-TOKEN";

function readCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

export const http = axios.create({
  baseURL: API_BASE,
  withCredentials: true,
});

http.interceptors.request.use((config) => {
  const method = (config.method ?? "get").toUpperCase();

  if (!["GET", "HEAD", "OPTIONS"].includes(method)) {
    const token = readCookie(CSRF_COOKIE);
    if (token) {
      config.headers.set(CSRF_HEADER, token);
    }
  }

  return config;
});

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail || problem.title || "İstek başarısız oldu.");
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }

  get fieldErrors(): Record<string, string[]> {
    return this.problem.errors ?? {};
  }
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;

  const axiosError = error as AxiosError<ProblemDetails>;

  if (axiosError.isAxiosError) {
    if (axiosError.response) {
      return new ApiError(axiosError.response.status, axiosError.response.data ?? {});
    }
    return new ApiError(0, {
      title: "Sunucuya ulaşılamıyor",
      detail: "API çalışmıyor olabilir. Backend'i başlatıp tekrar deneyin.",
    });
  }

  return new ApiError(0, { title: "Beklenmeyen hata", detail: String(error) });
}

const SESSION_ROUTES = ["/auth/login", "/auth/register", "/auth/refresh", "/auth/logout"];

type SessionListener = () => void;

let refreshed: SessionListener | null = null;
let lost: SessionListener | null = null;

export function onSessionRefreshed(listener: SessionListener): void {
  refreshed = listener;
}

export function onSessionLost(listener: SessionListener): void {
  lost = listener;
}

async function requestRefresh(): Promise<void> {
  const token = readCookie(CSRF_COOKIE);

  try {
    await axios.post(`${API_BASE}/auth/refresh`, null, {
      withCredentials: true,
      headers: token ? { [CSRF_HEADER]: token } : undefined,
    });
  } catch (error) {
    if ((error as AxiosError).response?.status !== 409) {
      throw error;
    }
  }
}

let inFlight: Promise<void> | null = null;

function refreshSession(): Promise<void> {
  inFlight ??= requestRefresh().finally(() => {
    inFlight = null;
  });

  return inFlight;
}

type RetriableConfig = InternalAxiosRequestConfig & { sessionRetry?: boolean };

http.interceptors.response.use(
  (response) => response,
  async (error) => {
    const axiosError = error as AxiosError<ProblemDetails>;
    const config = axiosError.config as RetriableConfig | undefined;

    const worthRetrying =
      axiosError.response?.status === 401 &&
      config !== undefined &&
      !config.sessionRetry &&
      !SESSION_ROUTES.some((route) => (config.url ?? "").includes(route));

    if (!worthRetrying) {
      return Promise.reject(toApiError(error));
    }

    config.sessionRetry = true;

    try {
      await refreshSession();
    } catch {
      lost?.();
      return Promise.reject(toApiError(error));
    }

    refreshed?.();

    return http.request(config);
  }
);
