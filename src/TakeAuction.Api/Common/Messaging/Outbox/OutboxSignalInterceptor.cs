using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class OutboxSignalInterceptor : SaveChangesInterceptor
{
    private readonly OutboxSignal _signal;

    private bool _queuedInThisSave;

    public OutboxSignalInterceptor(OutboxSignal signal) => _signal = signal;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _queuedInThisSave = HasNewMessages(eventData);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        _queuedInThisSave = HasNewMessages(eventData);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        NotifyIfQueued();

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        NotifyIfQueued();

        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _queuedInThisSave = false;

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _queuedInThisSave = false;

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static bool HasNewMessages(DbContextEventData eventData) =>
        eventData.Context is not null
        && eventData.Context.ChangeTracker
            .Entries<OutboxMessage>()
            .Any(entry => entry.State == EntityState.Added);

    private void NotifyIfQueued()
    {
        if (!_queuedInThisSave)
        {
            return;
        }

        _queuedInThisSave = false;
        _signal.Notify();
    }
}
