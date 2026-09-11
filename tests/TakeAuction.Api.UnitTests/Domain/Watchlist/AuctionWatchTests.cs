using TakeAuction.Api.Domain.Watchlist;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Domain.Watchlist;

public sealed class AuctionWatchTests
{
    [Fact]
    public void A_new_watch_has_not_been_reminded_yet()
    {
        var watch = AuctionWatch.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), TestHarness.Now);

        Assert.Null(watch.ClosingSoonNotifiedAtUtc);
        Assert.Equal(TestHarness.Now, watch.CreatedAtUtc);
    }

    [Fact]
    public void Refuses_a_watch_without_a_watcher() =>
        Assert.Throws<ArgumentException>(() => AuctionWatch.Start(Guid.Empty, Guid.CreateVersion7(), TestHarness.Now));

    [Fact]
    public void Refuses_a_watch_without_an_auction() =>
        Assert.Throws<ArgumentException>(() => AuctionWatch.Start(Guid.CreateVersion7(), Guid.Empty, TestHarness.Now));
}
