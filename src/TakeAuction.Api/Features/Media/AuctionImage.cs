namespace TakeAuction.Api.Features.Media;

public sealed record AuctionImage(string FileName, string ContentType, byte[] Content, DateTimeOffset CreatedAtUtc);
