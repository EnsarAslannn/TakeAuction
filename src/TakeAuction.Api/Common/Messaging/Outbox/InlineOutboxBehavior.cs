using MediatR;

namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class InlineOutboxBehavior<TRequest, TResponse>(
    OutboxSignalInterceptor signal, IServiceScopeFactory scopes, ILogger<InlineOutboxBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);
        if (!signal.HasQueuedMessages)
        {
            return response;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchBatchAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The request was committed; the scheduled sweep will retry outbox delivery.");
        }
        return response;
    }
}
