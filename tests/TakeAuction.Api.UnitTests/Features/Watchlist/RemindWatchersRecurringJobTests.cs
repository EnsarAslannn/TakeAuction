using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Features.Watchlist.RemindWatchers;

namespace TakeAuction.Api.UnitTests.Features.Watchlist;

public sealed class RemindWatchersRecurringJobTests
{
    private readonly IRecurringJobManager _manager = Substitute.For<IRecurringJobManager>();

    [Fact]
    public void Registers_the_reminder_sweep_under_a_stable_job_id()
    {
        Register(new JobOptions());

        _manager.Received(1).AddOrUpdate(
            RemindWatchersRecurringJob.JobId,
            Arg.Is<Job>(job =>
                job.Type == typeof(RemindWatchersJob)
                && job.Method.Name == nameof(RemindWatchersJob.RunAsync)),
            "* * * * *",
            Arg.Is<RecurringJobOptions>(options => options.TimeZone == TimeZoneInfo.Utc));
    }

    [Fact]
    public void Honours_a_configured_cron_expression()
    {
        Register(new JobOptions { RemindWatchersCron = "*/2 * * * *" });

        _manager.Received(1).AddOrUpdate(
            RemindWatchersRecurringJob.JobId,
            Arg.Any<Job>(),
            "*/2 * * * *",
            Arg.Any<RecurringJobOptions>());
    }

    private void Register(JobOptions options) =>
        new RemindWatchersRecurringJob(
            Options.Create(options),
            NullLogger<RemindWatchersRecurringJob>.Instance)
            .Register(_manager);
}
