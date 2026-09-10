using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Features.Auctions.OpenAuctions;

namespace TakeAuction.Api.UnitTests.Features.Auctions.OpenAuctions;

public sealed class ActivateAuctionsRecurringJobTests
{
    private readonly IRecurringJobManager _manager = Substitute.For<IRecurringJobManager>();

    [Fact]
    public void Registers_the_sweep_under_a_stable_job_id()
    {
        Register(new JobOptions());

        _manager.Received(1).AddOrUpdate(
            ActivateAuctionsRecurringJob.JobId,
            Arg.Is<Job>(job =>
                job.Type == typeof(ActivateAuctionsJob)
                && job.Method.Name == nameof(ActivateAuctionsJob.RunAsync)),
            "* * * * *",
            Arg.Any<RecurringJobOptions>());
    }

    [Fact]
    public void Honours_a_configured_cron_expression()
    {
        Register(new JobOptions { ActivateAuctionsCron = "*/5 * * * *" });

        _manager.Received(1).AddOrUpdate(
            ActivateAuctionsRecurringJob.JobId,
            Arg.Any<Job>(),
            "*/5 * * * *",
            Arg.Any<RecurringJobOptions>());
    }

    [Fact]
    public void Schedules_the_sweep_in_utc_so_it_never_shifts_with_the_server_time_zone()
    {
        Register(new JobOptions());

        _manager.Received(1).AddOrUpdate(
            ActivateAuctionsRecurringJob.JobId,
            Arg.Any<Job>(),
            Arg.Any<string>(),
            Arg.Is<RecurringJobOptions>(options => options.TimeZone == TimeZoneInfo.Utc));
    }

    [Fact]
    public void Keeps_its_job_id_distinct_from_the_closing_sweep()
    {
        Assert.NotEqual(
            TakeAuction.Api.Features.Auctions.ExpireAuctions.ExpireAuctionsRecurringJob.JobId,
            ActivateAuctionsRecurringJob.JobId);
    }

    private void Register(JobOptions options) =>
        new ActivateAuctionsRecurringJob(
            Options.Create(options),
            NullLogger<ActivateAuctionsRecurringJob>.Instance)
            .Register(_manager);
}
