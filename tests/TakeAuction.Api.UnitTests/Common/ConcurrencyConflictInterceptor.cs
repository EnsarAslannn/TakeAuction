using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TakeAuction.Api.UnitTests.Common;

public sealed class ConcurrencyConflictInterceptor : SaveChangesInterceptor
{
    private readonly int _conflictCount;
    private readonly Action? _onConflict;

    public ConcurrencyConflictInterceptor(int conflictCount, Action? onConflict = null)
    {
        _conflictCount = conflictCount;
        _onConflict = onConflict;
    }

    public int SaveAttempts { get; private set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SaveAttempts++;

        if (SaveAttempts <= _conflictCount)
        {
            _onConflict?.Invoke();

            throw new DbUpdateConcurrencyException(
                $"Simulated optimistic concurrency conflict on attempt {SaveAttempts}.");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
