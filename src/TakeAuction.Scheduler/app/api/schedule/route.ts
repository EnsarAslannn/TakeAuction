import { isAuthorized, parseTimer } from "../../../lib/jobs";
import { schedule } from "../../../lib/schedule";

export async function POST(request: Request) {
  if (!isAuthorized(request, process.env.DURABLE_JOBS_SECRET)) {
    return new Response(null, { status: 401 });
  }
  const timer = parseTimer(await request.json().catch(() => null));
  if (!timer) return new Response(null, { status: 400 });
  return Response.json({ runId: await schedule(timer) }, { status: 202 });
}
