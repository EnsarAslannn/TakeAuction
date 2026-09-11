using MassTransit;
using TakeAuction.Api.Common.Observability;

namespace TakeAuction.Api.Common.Messaging;

public sealed class DeadLetterObserver : IReceiveObserver
{
    private readonly TakeAuctionTelemetry _telemetry;
    private readonly ILogger<DeadLetterObserver> _logger;

    public DeadLetterObserver(TakeAuctionTelemetry telemetry, ILogger<DeadLetterObserver> logger)
    {
        _telemetry = telemetry;
        _logger = logger;
    }

    public Task PreReceive(ReceiveContext context) => Task.CompletedTask;

    public Task PostReceive(ReceiveContext context) => Task.CompletedTask;

    public Task PostConsume<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType)
        where T : class =>
        Task.CompletedTask;

    public Task ConsumeFault<T>(
        ConsumeContext<T> context,
        TimeSpan duration,
        string consumerType,
        Exception exception)
        where T : class
    {
        var queue = QueueOf(context.ReceiveContext);

        _telemetry.MessageDeadLettered(queue, typeof(T).Name);

        _logger.LogError(
            exception,
            "{ConsumerType} gave up on {MessageType} {MessageId} after its retries; the message is parked on {Queue}_error",
            consumerType,
            typeof(T).Name,
            context.MessageId,
            queue);

        return Task.CompletedTask;
    }

    public Task ReceiveFault(ReceiveContext context, Exception exception)
    {
        var queue = QueueOf(context);

        _telemetry.MessageDeadLettered(queue, "unreadable");

        _logger.LogError(
            exception,
            "A message on {Queue} could not be read or dispatched and was faulted before any consumer saw it",
            queue);

        return Task.CompletedTask;
    }

    private static string QueueOf(ReceiveContext context) =>
        context.InputAddress.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()
        ?? "unknown";
}
