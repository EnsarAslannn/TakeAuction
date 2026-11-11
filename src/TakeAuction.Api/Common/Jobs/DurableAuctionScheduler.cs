using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace TakeAuction.Api.Common.Jobs;

public sealed class DurableAuctionScheduler(
    HttpClient client, IOptions<DurableJobsOptions> options, ILogger<DurableAuctionScheduler> logger)
{
    public bool Enabled => options.Value.Enabled;

    public async Task ScheduleAsync(string action, Guid auctionId, DateTimeOffset dueAtUtc)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.SchedulerUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.Secret);
        request.Content = JsonContent.Create(new { action, auctionId, dueAtUtc });
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Durable {Action} scheduling for auction {AuctionId} returned HTTP {Status}; the next sweep will repair it",
                    action, auctionId, (int)response.StatusCode);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // The auction is already committed. Periodic reconciliation handles missed timers.
            logger.LogWarning("Durable {Action} scheduling for auction {AuctionId} failed; the next sweep will repair it",
                action, auctionId);
        }
    }
}
