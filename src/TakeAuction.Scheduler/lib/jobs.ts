import { createHash, timingSafeEqual } from "node:crypto";

export type AuctionTimer = { action: "open" | "close"; auctionId: string; dueAtUtc: string };
export type Job = { action: "open" | "close" | "remind" | "sweep" | "maintenance"; auctionId?: string };

const digest = (value: string) => createHash("sha256").update(value).digest();

export function isAuthorized(request: Request, secret: string | undefined) {
  return Boolean(secret && secret.length >= 32 &&
    timingSafeEqual(digest(request.headers.get("authorization") ?? ""), digest(`Bearer ${secret}`)));
}

export function parseTimer(value: unknown): AuctionTimer | null {
  if (typeof value !== "object" || !value) return null;
  const { action, auctionId, dueAtUtc } = value as Partial<AuctionTimer>;
  if ((action !== "open" && action !== "close") ||
    typeof auctionId !== "string" || !/^[a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12}$/i.test(auctionId) ||
    typeof dueAtUtc !== "string" || !Number.isFinite(Date.parse(dueAtUtc))) return null;
  return { action, auctionId: auctionId.toLowerCase(), dueAtUtc: new Date(dueAtUtc).toISOString() };
}

export async function callApi(job: Job) {
  const url = process.env.TAKEAUCTION_API_JOB_URL;
  const secret = process.env.DURABLE_JOBS_SECRET;
  if (!url || !secret) throw new Error("TakeAuction job delivery is not configured");
  return fetch(url, {
    method: "POST",
    redirect: "error",
    headers: { authorization: `Bearer ${secret}`, "content-type": "application/json" },
    body: JSON.stringify(job),
    signal: AbortSignal.timeout(20_000),
  });
}
