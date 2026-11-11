import { callApi, isAuthorized } from "../../../lib/jobs";

export async function POST(request: Request) {
  if (!isAuthorized(request, process.env.CRON_SECRET)) return new Response(null, { status: 401 });
  const result = await callApi({ action: "maintenance" });
  if (!result.ok) throw new Error(`TakeAuction maintenance returned HTTP ${result.status}`);
  return new Response(null, { status: 204 });
}
