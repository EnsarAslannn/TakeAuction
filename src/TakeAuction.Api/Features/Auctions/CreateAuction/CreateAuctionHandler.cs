using MediatR;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions.CreateAuction;

public sealed class CreateAuctionHandler : IRequestHandler<CreateAuctionCommand, CreateAuctionResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IPublisher _publisher;
    private readonly ILogger<CreateAuctionHandler> _logger;

    public CreateAuctionHandler(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IPublisher publisher,
        ILogger<CreateAuctionHandler> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<CreateAuctionResponse> Handle(
        CreateAuctionCommand command,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var auction = Auction.Create(
            command.SellerId,
            command.Title,
            command.Description,
            command.StartingPrice,
            command.MinimumBidIncrement,
            command.StartsAtUtc,
            command.EndsAtUtc,
            now);

        await _dbContext.Auctions.AddAsync(auction, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Auction {AuctionId} created by seller {SellerId} ending at {EndsAtUtc}",
            auction.Id,
            auction.SellerId,
            auction.EndsAtUtc);

        await _publisher.Publish(
            new AuctionCreatedEvent(
                auction.Id,
                auction.SellerId,
                auction.StartingPrice,
                auction.Status.ToString(),
                auction.EndsAtUtc,
                now),
            cancellationToken);

        return new CreateAuctionResponse(
            auction.Id,
            auction.Status.ToString(),
            auction.StartsAtUtc,
            auction.EndsAtUtc,
            auction.CreatedAtUtc);
    }
}
