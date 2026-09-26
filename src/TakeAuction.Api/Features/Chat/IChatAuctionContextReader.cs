using MediatR;
using TakeAuction.Api.Features.Auctions.GetAuctionById;

namespace TakeAuction.Api.Features.Chat;

public interface IChatAuctionContextReader
{
    Task<ChatAuctionContext?> FindAsync(Guid auctionId, CancellationToken cancellationToken);
}

public sealed class ChatAuctionContextReader(ISender sender) : IChatAuctionContextReader
{
    public async Task<ChatAuctionContext?> FindAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        var auction = await sender.Send(new GetAuctionByIdQuery(auctionId), cancellationToken);
        return auction is null
            ? null
            : new ChatAuctionContext(
                auction.Id,
                auction.Title,
                auction.Status,
                auction.CurrentPrice,
                auction.MinimumAcceptableBid,
                auction.EndsAtUtc);
    }
}
