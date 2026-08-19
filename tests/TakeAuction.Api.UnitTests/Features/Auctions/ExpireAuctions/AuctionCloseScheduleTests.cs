using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions.ExpireAuctions;

public sealed class AuctionCloseScheduleTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();

    private readonly IBackgroundJobClient _jobs = Substitute.For<IBackgroundJobClient>();
    private readonly AuctionCloseSchedule _schedule;

    public AuctionCloseScheduleTests() =>
        _schedule = new AuctionCloseSchedule(_jobs, NullLogger<AuctionCloseSchedule>.Instance);

    [Fact]
    public void Books_the_close_for_the_second_the_lot_is_due()
    {
        _schedule.ScheduleClose(AuctionId, TestHarness.Now.AddHours(3), TestHarness.Now);

        _jobs.Received(1).Create(
            Arg.Is<Job>(job => job.Type == typeof(CloseAuctionJob) && job.Args[0].Equals(AuctionId)),
            Arg.Is<ScheduledState>(state => IsAbout(state, TimeSpan.FromHours(3))));
    }

    [Fact]
    public void Books_a_lot_whose_close_has_already_passed_for_right_now()
    {
        _schedule.ScheduleClose(AuctionId, TestHarness.Now.AddHours(-1), TestHarness.Now);

        _jobs.Received(1).Create(
            Arg.Any<Job>(),
            Arg.Is<ScheduledState>(state => IsAbout(state, TimeSpan.Zero)));
    }

    [Fact]
    public void Books_another_close_rather_than_editing_the_one_already_in_the_queue()
    {
        _schedule.ScheduleClose(AuctionId, TestHarness.Now.AddMinutes(1), TestHarness.Now);
        _schedule.ScheduleClose(AuctionId, TestHarness.Now.AddMinutes(2), TestHarness.Now);

        _jobs.Received(2).Create(Arg.Any<Job>(), Arg.Any<ScheduledState>());
    }

    private static bool IsAbout(ScheduledState state, TimeSpan delay) =>
        (state.EnqueueAt - DateTime.UtcNow - delay).Duration() < TimeSpan.FromSeconds(30);
}
