using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Observability;

namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class OutboxBacklogSampler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TakeAuctionTelemetry _telemetry;
    private readonly IOptions<OutboxOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxBacklogSampler> _logger;

    private long _lastDeadLetters;

    public OutboxBacklogSampler(
        IServiceScopeFactory scopeFactory,
        TakeAuctionTelemetry telemetry,
        IOptions<OutboxOptions> options,
        TimeProvider timeProvider,
        ILogger<OutboxBacklogSampler> logger)
    {
        _scopeFactory = scopeFactory;
        _telemetry = telemetry;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_options.Value.BacklogSampleIntervalSeconds),
            _timeProvider);

        do
        {
            await SampleAsync(stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    public async Task SampleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var probe = scope.ServiceProvider.GetRequiredService<OutboxBacklogProbe>();

            var backlog = await probe.MeasureAsync(cancellationToken);

            _telemetry.OutboxBacklogSampled(backlog);

            if (backlog.DeadLetters > _lastDeadLetters)
            {
                _logger.LogError(
                    "{DeadLetters} outbox message(s) have exhausted their attempts and will not be published without intervention",
                    backlog.DeadLetters);
            }

            _lastDeadLetters = backlog.DeadLetters;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not sample the outbox backlog; the gauges keep their last reading");
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
