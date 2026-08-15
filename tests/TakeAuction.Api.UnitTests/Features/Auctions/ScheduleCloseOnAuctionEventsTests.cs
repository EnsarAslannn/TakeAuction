using NSubstitute;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.Features.Auctions.PlaceBid;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class ScheduleCloseOnAuctionEventsTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();

    private readonly IAuctionCloseSchedule _schedule = Substitute.For<IAuctionCloseSchedule>();

    [Fact]
    public async Task A_new_lot_is_booked_to_close_at_its_end_time()
    {
        var handler = new ScheduleCloseOnAuctionCreated(_schedule);
        var endsAt = TestHarness.Now.AddDays(2);

        await handler.Handle(
            new AuctionCreatedEvent(AuctionId, Guid.CreateVersion7(), 100m, "Active", endsAt, TestHarness.Now),
            CancellationToken.None);

        _schedule.Received(1).ScheduleClose(AuctionId, endsAt, TestHarness.Now);
    }

    [Fact]
    public async Task A_bid_that_pushed_the_close_out_books_the_new_time()
    {
        var handler = new RescheduleCloseOnAuctionExtended(_schedule);
        var extendedTo = TestHarness.Now.AddSeconds(60);

        await handler.Handle(BidEvent(extended: true, endsAtUtc: extendedTo), CancellationToken.None);

        _schedule.Received(1).ScheduleClose(AuctionId, extendedTo, TestHarness.Now);
    }

    [Fact]
    public async Task An_ordinary_bid_leaves_the_booking_alone()
    {
        var handler = new RescheduleCloseOnAuctionExtended(_schedule);

        await handler.Handle(
            BidEvent(extended: false, endsAtUtc: TestHarness.Now.AddDays(2)),
            CancellationToken.None);

        _schedule.DidNotReceive().ScheduleClose(
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>());
    }

    private static BidPlacedEvent BidEvent(bool extended, DateTimeOffset endsAtUtc) => new(
        AuctionId,
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        150m,
        false,
        100m,
        null,
        endsAtUtc,
        extended,
        TestHarness.Now);
}
