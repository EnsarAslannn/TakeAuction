using Microsoft.Extensions.Options;

namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class OutboxDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxSignal _signal;
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<OutboxDispatcherService> _logger;

    public OutboxDispatcherService(
        IServiceScopeFactory scopeFactory,
        OutboxSignal signal,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _signal = signal;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(_options.Value.PollIntervalSeconds);

        _logger.LogInformation(
            "Outbox dispatcher started; sweeping every {PollInterval} and on every commit",
            pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _signal.WaitAsync(pollInterval, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await DrainAsync(stoppingToken);
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();

                var sweep = await dispatcher.DispatchBatchAsync(cancellationToken);

                if (!sweep.MoreLikely)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox sweep failed; retrying on the next tick");
        }
    }
}
