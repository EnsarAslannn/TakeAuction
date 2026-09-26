using MediatR;
using TakeAuction.Api.Features.Auctions.GetAuctionById;
using TakeAuction.Api.Features.Auctions.GetBidStanding;

namespace TakeAuction.Api.Features.Chat;

public interface IChatAuctionContextReader
{
    Task<ChatAuctionContext?> FindAsync(Guid auctionId, CancellationToken cancellationToken);

    Task<ChatBidStandingContext?> FindStandingAsync(
        Guid auctionId,
        Guid bidderId,
        CancellationToken cancellationToken);
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

    public async Task<ChatBidStandingContext?> FindStandingAsync(
        Guid auctionId,
        Guid bidderId,
        CancellationToken cancellationToken)
    {
        var standing = await sender.Send(new GetBidStandingQuery(auctionId, bidderId), cancellationToken);
        if (standing is null)
        {
            return null;
        }

        var auction = await sender.Send(new GetAuctionByIdQuery(auctionId), cancellationToken);
        return auction is null
            ? null
            : new ChatBidStandingContext(
                auction.Id,
                auction.Title,
                auction.CurrentPrice,
                standing.IsLeading,
                standing.MaxAmount,
                standing.MinimumAcceptableBid);
    }
}
