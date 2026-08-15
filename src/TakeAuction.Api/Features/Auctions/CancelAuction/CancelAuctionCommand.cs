using MediatR;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions.CancelAuction;

public sealed record CancelAuctionCommand(Guid AuctionId, Guid SellerId) : IRequest<CancelAuctionResult>;

public sealed record CancelAuctionResponse(
    Guid Id,
    string Status,
    DateTimeOffset CancelledAtUtc);

public sealed record CancelAuctionResult(CancelRejection Rejection, CancelAuctionResponse? Response)
{
    public bool Succeeded => Rejection == CancelRejection.None;

    public static CancelAuctionResult Accepted(CancelAuctionResponse response) =>
        new(CancelRejection.None, response);

    public static CancelAuctionResult Rejected(CancelRejection rejection) => new(rejection, null);
}
