import { callApi, isAuthorized, parseTimer } from "../../../lib/jobs";
import { schedule } from "../../../lib/schedule";

export async function POST(request: Request) {
  if (!isAuthorized(request, process.env.CRON_SECRET)) return new Response(null, { status: 401 });
  const result = await callApi({ action: "sweep" });
  if (!result.ok) throw new Error(`TakeAuction sweep returned HTTP ${result.status}`);
  const timers: unknown[] = await result.json();
  for (let offset = 0; offset < timers.length; offset += 10) {
    await Promise.all(timers.slice(offset, offset + 10).map((value) => {
      const timer = parseTimer(value);
      if (!timer) throw new Error("TakeAuction sweep returned an invalid timer");
      return schedule(timer);
    }));
  }
  return new Response(null, { status: 204 });
}
