import { FatalError, RetryableError, sleep } from "workflow";
import { callApi, type AuctionTimer, type Job } from "../lib/jobs";

export async function auctionTimer(timer: AuctionTimer) {
  "use workflow";
  const due = Date.parse(timer.dueAtUtc);
  if (timer.action === "close" && due > Date.now()) {
    const remindAt = due - 5 * 60_000;
    if (remindAt > Date.now()) await sleep(new Date(remindAt));
    // A reminder failure shouldn't prevent an auction from closing on time.
    try { await deliver({ action: "remind" }); } catch { /* reconciliation will retry */ }
  }
  if (due > Date.now()) await sleep(new Date(due));
  await deliver({ action: timer.action, auctionId: timer.auctionId });
}

async function deliver(job: Job) {
  "use step";
  const response = await callApi(job);
  if (response.ok) return;
  if ([400, 401, 403, 404].includes(response.status)) {
    throw new FatalError(`TakeAuction rejected the ${job.action} job (HTTP ${response.status})`);
  }
  throw new RetryableError(`TakeAuction ${job.action} job returned HTTP ${response.status}`, { retryAfter: "10s" });
}
