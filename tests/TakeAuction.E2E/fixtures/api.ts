import { expect, type APIRequestContext } from "@playwright/test";

export const API_PREFIX = "/api/v1";
export const CSRF_COOKIE = "takeauction_csrf";
export const CSRF_HEADER = "X-CSRF-TOKEN";
export const DEFAULT_PASSWORD = "E2ePass!2026";

export type Role = "Bidder" | "Seller";

export interface Account {
  email: string;
  password: string;
  displayName: string;
  role: Role;
}

export interface AuctionDetail {
  id: string;
  title: string;
  description: string;
  startingPrice: number;
  currentPrice: number;
  minimumBidIncrement: number;
  minimumAcceptableBid: number;
  bidCount: number;
  status: "Scheduled" | "Active" | "Ended" | "Cancelled";
  startsAtUtc: string;
  endsAtUtc: string;
  sellerId: string;
  sellerDisplayName: string;
}

export interface CreatedAuction {
  id: string;
  title: string;
  startingPrice: number;
  minimumBidIncrement: number;
}

let sequence = 0;

function unique(prefix: string): string {
  sequence += 1;

  return `${prefix}-${Date.now().toString(36)}-${sequence}`;
}

export function newAccount(role: Role): Account {
  const handle = unique(role.toLowerCase());

  return {
    email: `${handle}@takeauction.test`,
    password: DEFAULT_PASSWORD,
    displayName: `E2E ${role} ${sequence}`,
    role,
  };
}

export function uniqueTitle(prefix = "E2E lot"): string {
  return `${prefix} ${unique("ref")}`;
}

/**
 * The API only enforces the double-submit token once a session cookie exists, but reading
 * it back for every mutation is exactly what the SPA's axios interceptor does.
 */
async function csrfHeaders(context: APIRequestContext): Promise<Record<string, string>> {
  const { cookies } = await context.storageState();
  const token = cookies.find((cookie) => cookie.name === CSRF_COOKIE)?.value;

  return token ? { [CSRF_HEADER]: token } : {};
}

export async function register(context: APIRequestContext, account: Account): Promise<Account> {
  const response = await context.post(`${API_PREFIX}/auth/register`, {
    headers: await csrfHeaders(context),
    data: {
      email: account.email,
      displayName: account.displayName,
      password: account.password,
      role: account.role,
    },
  });

  expect(response.status(), await response.text()).toBe(201);

  return account;
}

export function registerBidder(context: APIRequestContext): Promise<Account> {
  return register(context, newAccount("Bidder"));
}

export function registerSeller(context: APIRequestContext): Promise<Account> {
  return register(context, newAccount("Seller"));
}

export async function login(context: APIRequestContext, account: Account): Promise<void> {
  const response = await context.post(`${API_PREFIX}/auth/login`, {
    headers: await csrfHeaders(context),
    data: { email: account.email, password: account.password },
  });

  expect(response.status(), await response.text()).toBe(200);
}

interface CreateAuctionOptions {
  title?: string;
  startingPrice?: number;
  minimumBidIncrement?: number;
  startsAtUtc?: string;
  endsAtUtc?: string;
}

async function createAuction(
  context: APIRequestContext,
  options: CreateAuctionOptions
): Promise<CreatedAuction> {
  const title = options.title ?? uniqueTitle();
  const startingPrice = options.startingPrice ?? 1000;
  const minimumBidIncrement = options.minimumBidIncrement ?? 50;

  const response = await context.post(`${API_PREFIX}/auctions`, {
    headers: await csrfHeaders(context),
    data: {
      title,
      description: "Bir uçtan uca test tarafından açılan parça. Salonun canlı davranışını doğrular.",
      startingPrice,
      minimumBidIncrement,
      startsAtUtc: options.startsAtUtc ?? new Date(Date.now() - 30_000).toISOString(),
      endsAtUtc: options.endsAtUtc ?? new Date(Date.now() + 3 * 60 * 60 * 1000).toISOString(),
    },
  });

  expect(response.status(), await response.text()).toBe(201);

  const created = (await response.json()) as { id: string };

  return { id: created.id, title, startingPrice, minimumBidIncrement };
}

/**
 * Creates a lot whose window is already open. The create validator tolerates a start up to a
 * minute in the past, which is what puts the auction straight into Active without waiting.
 */
export function createOpenAuction(
  context: APIRequestContext,
  options: CreateAuctionOptions = {}
): Promise<CreatedAuction> {
  return createAuction(context, options);
}

export function createScheduledAuction(
  context: APIRequestContext,
  options: CreateAuctionOptions = {}
): Promise<CreatedAuction> {
  return createAuction(context, {
    ...options,
    startsAtUtc: new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString(),
    endsAtUtc: new Date(Date.now() + 6 * 60 * 60 * 1000).toISOString(),
  });
}

/** Registers a throwaway seller on its own context and hands back an open lot. */
export async function seedOpenAuction(
  context: APIRequestContext,
  options: CreateAuctionOptions = {}
): Promise<CreatedAuction> {
  await registerSeller(context);

  return createOpenAuction(context, options);
}

export async function getAuction(context: APIRequestContext, auctionId: string): Promise<AuctionDetail> {
  const response = await context.get(`${API_PREFIX}/auctions/${auctionId}`);

  expect(response.status(), await response.text()).toBe(200);

  return (await response.json()) as AuctionDetail;
}

export async function placeBid(
  context: APIRequestContext,
  auctionId: string,
  amount: number
): Promise<void> {
  const response = await context.post(`${API_PREFIX}/auctions/${auctionId}/bids`, {
    headers: await csrfHeaders(context),
    data: { amount },
  });

  expect(response.status(), await response.text()).toBe(200);
}
