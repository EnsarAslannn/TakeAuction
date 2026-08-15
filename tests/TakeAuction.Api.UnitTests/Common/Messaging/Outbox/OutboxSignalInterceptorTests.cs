using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Common.Messaging.Outbox;

public sealed class OutboxSignalInterceptorTests : IDisposable
{
    private static readonly TimeSpan NoWait = TimeSpan.Zero;

    private readonly OutboxSignal _signal = new();
    private readonly AppDbContext _dbContext;

    public OutboxSignalInterceptorTests() =>
        _dbContext = TestHarness.CreateDbContext(interceptors: new OutboxSignalInterceptor(_signal));

    public void Dispose()
    {
        _dbContext.Dispose();
        _signal.Dispose();
    }

    [Fact]
    public async Task Nudges_the_dispatcher_once_the_message_is_actually_committed()
    {
        TestHarness.CreateOutbox(_dbContext).Enqueue(Event(), TestHarness.Now);

        Assert.False(await _signal.WaitAsync(NoWait, CancellationToken.None));

        await _dbContext.SaveChangesAsync();

        Assert.True(await _signal.WaitAsync(NoWait, CancellationToken.None));
    }

    [Fact]
    public async Task Stays_quiet_for_a_save_that_carries_no_message()
    {
        _dbContext.Users.Add(User.Create(
            "quiet@takeauction.test",
            "Quiet",
            "not-a-real-hash",
            UserRole.Bidder));

        await _dbContext.SaveChangesAsync();

        Assert.False(await _signal.WaitAsync(NoWait, CancellationToken.None));
    }

    [Fact]
    public async Task Does_not_carry_a_pending_nudge_into_the_next_save()
    {
        TestHarness.CreateOutbox(_dbContext).Enqueue(Event(), TestHarness.Now);
        await _dbContext.SaveChangesAsync();

        Assert.True(await _signal.WaitAsync(NoWait, CancellationToken.None));

        await _dbContext.SaveChangesAsync();

        Assert.False(await _signal.WaitAsync(NoWait, CancellationToken.None));
    }

    private static BidPlacedIntegrationEvent Event() => new(
        Guid.CreateVersion7(),
        "Rare stamp collection",
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        150m,
        false,
        100m,
        null,
        TestHarness.Now.AddDays(2),
        false,
        TestHarness.Now);
}
