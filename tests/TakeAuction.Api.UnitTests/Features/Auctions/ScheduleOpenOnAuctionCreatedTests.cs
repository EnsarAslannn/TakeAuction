using NSubstitute;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.Features.Auctions.OpenAuctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class ScheduleOpenOnAuctionCreatedTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();

    private readonly IAuctionOpenSchedule _schedule = Substitute.For<IAuctionOpenSchedule>();

    [Fact]
    public async Task A_lot_that_opens_later_is_booked_to_open_at_its_start_time()
    {
        var startsAt = TestHarness.Now.AddHours(6);

        await Handle(nameof(AuctionStatus.Scheduled), startsAt);

        _schedule.Received(1).ScheduleOpen(AuctionId, startsAt, TestHarness.Now);
    }

    [Fact]
    public async Task A_lot_that_is_already_open_needs_no_booking()
    {
        await Handle(nameof(AuctionStatus.Active), TestHarness.Now);

        _schedule.DidNotReceive().ScheduleOpen(
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>());
    }

    private Task Handle(string status, DateTimeOffset startsAt) =>
        new ScheduleOpenOnAuctionCreated(_schedule).Handle(
            new AuctionCreatedEvent(
                AuctionId,
                Guid.CreateVersion7(),
                100m,
                status,
                startsAt,
                startsAt.AddDays(1),
                TestHarness.Now),
            CancellationToken.None);
}
