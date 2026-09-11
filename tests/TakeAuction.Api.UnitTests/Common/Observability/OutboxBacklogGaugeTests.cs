using TakeAuction.Api.Common.Observability;

namespace TakeAuction.Api.UnitTests.Common.Observability;

public sealed class OutboxBacklogGaugeTests
{
    [Fact]
    public void Reports_nothing_before_the_first_sample_rather_than_a_reassuring_zero()
    {
        var telemetry = TestHarness.CreateTelemetry();
        using var metrics = new MetricCollector(telemetry.Meter);

        metrics.Observe();

        Assert.Empty(metrics.For("takeauction.outbox.pending"));
        Assert.Empty(metrics.For("takeauction.outbox.dead_letters"));
        Assert.Empty(metrics.For("takeauction.outbox.oldest_pending_age"));
    }

    [Fact]
    public void Reports_the_latest_sample_on_every_scrape()
    {
        var telemetry = TestHarness.CreateTelemetry();
        using var metrics = new MetricCollector(telemetry.Meter);

        telemetry.OutboxBacklogSampled(new OutboxBacklog(Pending: 7, DeadLetters: 2, OldestPendingAgeSeconds: 42.5));
        metrics.Observe();

        Assert.Equal(7, metrics.Total("takeauction.outbox.pending"));
        Assert.Equal(2, metrics.Total("takeauction.outbox.dead_letters"));
        Assert.Equal(42.5, metrics.Total("takeauction.outbox.oldest_pending_age"));
    }

    [Fact]
    public void A_newer_sample_replaces_the_older_one()
    {
        var telemetry = TestHarness.CreateTelemetry();

        telemetry.OutboxBacklogSampled(new OutboxBacklog(9, 1, 10));
        telemetry.OutboxBacklogSampled(new OutboxBacklog(0, 0, 0));

        using var metrics = new MetricCollector(telemetry.Meter);
        metrics.Observe();

        Assert.Equal(0, metrics.Total("takeauction.outbox.pending"));
        Assert.Equal(0, metrics.Total("takeauction.outbox.dead_letters"));
    }
}
