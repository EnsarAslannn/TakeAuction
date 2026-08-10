using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;

namespace TakeAuction.Api.UnitTests.Features.Auctions.ExpireAuctions;

public sealed class ExpireAuctionsRecurringJobTests
{
    private readonly IRecurringJobManager _manager = Substitute.For<IRecurringJobManager>();

    [Fact]
    public void Registers_the_sweep_under_a_stable_job_id()
    {
        Register(new JobOptions());

        _manager.Received(1).AddOrUpdate(
            ExpireAuctionsRecurringJob.JobId,
            Arg.Is<Job>(job =>
                job.Type == typeof(ExpireAuctionsJob)
                && job.Method.Name == nameof(ExpireAuctionsJob.RunAsync)),
            "* * * * *",
            Arg.Any<RecurringJobOptions>());
    }

    [Fact]
    public void Honours_a_configured_cron_expression()
    {
        Register(new JobOptions { ExpireAuctionsCron = "*/5 * * * *" });

        _manager.Received(1).AddOrUpdate(
            ExpireAuctionsRecurringJob.JobId,
            Arg.Any<Job>(),
            "*/5 * * * *",
            Arg.Any<RecurringJobOptions>());
    }

    [Fact]
    public void Schedules_the_sweep_in_utc_so_it_never_shifts_with_the_server_time_zone()
    {
        Register(new JobOptions());

        _manager.Received(1).AddOrUpdate(
            ExpireAuctionsRecurringJob.JobId,
            Arg.Any<Job>(),
            Arg.Any<string>(),
            Arg.Is<RecurringJobOptions>(options => options.TimeZone == TimeZoneInfo.Utc));
    }

    private void Register(JobOptions options) =>
        new ExpireAuctionsRecurringJob(
            Options.Create(options),
            NullLogger<ExpireAuctionsRecurringJob>.Instance)
            .Register(_manager);
}
