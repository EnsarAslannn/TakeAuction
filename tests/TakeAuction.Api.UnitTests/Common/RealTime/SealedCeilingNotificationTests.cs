using TakeAuction.Api.Common.RealTime;

namespace TakeAuction.Api.UnitTests.Common.RealTime;

public sealed class SealedCeilingNotificationTests
{
    [Theory]
    [InlineData(typeof(BidPlacedNotification))]
    [InlineData(typeof(AuctionStatusChangedNotification))]
    [InlineData(typeof(OutbidNotification))]
    public void No_broadcast_carries_a_bidder_ceiling(Type notification)
    {
        var leaked = notification
            .GetProperties()
            .Select(property => property.Name)
            .Where(name => name.Contains("Max", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Ceiling", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(leaked);
    }
}
