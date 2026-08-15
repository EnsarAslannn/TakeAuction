using TakeAuction.Api.Common.Messaging.Outbox;

namespace TakeAuction.Api.UnitTests.Common.Messaging.Outbox;

public sealed class OutboxSignalTests
{
    private static readonly TimeSpan NoWait = TimeSpan.Zero;
    private static readonly TimeSpan ShortWait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Wakes_a_waiter_that_is_already_listening()
    {
        using var signal = new OutboxSignal();

        var waiting = signal.WaitAsync(ShortWait, CancellationToken.None);
        signal.Notify();

        Assert.True(await waiting);
    }

    [Fact]
    public async Task Remembers_a_notification_that_arrived_before_anyone_waited()
    {
        using var signal = new OutboxSignal();

        signal.Notify();

        Assert.True(await signal.WaitAsync(NoWait, CancellationToken.None));
    }

    [Fact]
    public async Task Collapses_a_burst_into_a_single_wake_up()
    {
        using var signal = new OutboxSignal();

        for (var i = 0; i < 50; i++)
        {
            signal.Notify();
        }

        Assert.True(await signal.WaitAsync(NoWait, CancellationToken.None));
        Assert.False(await signal.WaitAsync(NoWait, CancellationToken.None));
    }

    [Fact]
    public async Task Falls_through_on_the_timeout_so_the_sweep_still_runs()
    {
        using var signal = new OutboxSignal();

        Assert.False(await signal.WaitAsync(NoWait, CancellationToken.None));
    }

    [Fact]
    public async Task Returns_quietly_on_shutdown_instead_of_throwing()
    {
        using var signal = new OutboxSignal();
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        Assert.False(await signal.WaitAsync(ShortWait, cancellation.Token));
    }
}
