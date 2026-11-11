using MassTransit;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Notifications;

namespace TakeAuction.Api.Common.Messaging.Outbox;

public interface IOutboxTransport
{
    Task PublishAsync(object message, Type contract, CancellationToken cancellationToken);
}

public sealed class BrokerOutboxTransport(IPublishEndpoint publisher) : IOutboxTransport
{
    public Task PublishAsync(object message, Type contract, CancellationToken cancellationToken) =>
        publisher.Publish(message, contract, cancellationToken);
}

public sealed class InlineOutboxTransport(IServiceScopeFactory scopes) : IOutboxTransport
{
    public async Task PublishAsync(object message, Type contract, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        switch (message)
        {
            case AuctionCreatedIntegrationEvent created:
                await services.GetRequiredService<BroadcastAuctionCreatedConsumer>().HandleAsync(created, cancellationToken);
                break;
            case AuctionOpenedIntegrationEvent opened:
                await services.GetRequiredService<BroadcastAuctionOpenedConsumer>().HandleAsync(opened, cancellationToken);
                break;
            case AuctionCancelledIntegrationEvent cancelled:
                await services.GetRequiredService<BroadcastAuctionCancelledConsumer>().HandleAsync(cancelled, cancellationToken);
                break;
            case AuctionEndedIntegrationEvent ended:
                await services.GetRequiredService<NotifyAuctionOutcomeConsumer>().HandleAsync(ended, cancellationToken);
                await services.GetRequiredService<BroadcastAuctionEndedConsumer>().HandleAsync(ended, cancellationToken);
                break;
            case BidPlacedIntegrationEvent bid:
                await services.GetRequiredService<BroadcastBidPlacedConsumer>().HandleAsync(bid, cancellationToken);
                break;
            case WatchedAuctionClosingSoonIntegrationEvent reminder:
                await services.GetRequiredService<NotifyWatcherClosingSoonConsumer>().HandleAsync(reminder, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"No inline handler is registered for outbox contract '{contract.Name}'.");
        }
    }
}
