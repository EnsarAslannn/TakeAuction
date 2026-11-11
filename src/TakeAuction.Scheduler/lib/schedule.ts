import { HookConflictError } from "workflow/errors";
import { start } from "workflow/api";
import { auctionTimer } from "../workflows/auction-timer";
import type { AuctionTimer } from "./jobs";

export async function schedule(timer: AuctionTimer) {
  try {
    const run = await start(auctionTimer, [timer], {
      experimental_hook: { token: `takeauction:${timer.action}:${timer.auctionId}:${timer.dueAtUtc}` },
    });
    return run.runId;
  } catch (error) {
    if (HookConflictError.is(error)) return error.conflictingRunId;
    throw error;
  }
}
