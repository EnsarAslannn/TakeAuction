namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class OutboxSignal : IDisposable
{
    private readonly SemaphoreSlim _pending = new(0, 1);

    public void Notify()
    {
        // A single outstanding permit is enough: the dispatcher always drains everything it
        // finds, so a second notification arriving before the first wakes it up would only
        // buy an empty extra sweep.
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
