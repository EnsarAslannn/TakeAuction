namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class OutboxSignal : IDisposable
{
    private readonly SemaphoreSlim _pending = new(0, 1);

    public void Notify()
    {
        try
        {
            _pending.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return await _pending.WaitAsync(timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose() => _pending.Dispose();
}
