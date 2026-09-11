using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Common.Messaging;
using TakeAuction.Api.Common.Observability;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Common.Messaging;

public sealed class DeadLetterObserverTests
{
    private const string Instrument = "takeauction.messaging.dead_letters";
    private const int Retries = 3;

    [Fact]
    public async Task Counts_a_message_once_when_its_consumer_runs_out_of_retries()
    {
        var telemetry = TestHarness.CreateTelemetry();
        using var metrics = new MetricCollector(telemetry.Meter);

        await using var provider = BuildBus<RefusingConsumer>(telemetry);
        var harness = await StartAsync(provider);

        await harness.Bus.Publish(new PoisonMessage(Guid.CreateVersion7()));
        await harness.Bus.Publish(new PoisonMessage(Guid.CreateVersion7()));

        Assert.True(await harness.Consumed.Any<PoisonMessage>(context => context.Exception is not null));
        await WaitForAsync(() => metrics.Total(Instrument) >= 2);

        Assert.Equal(2, metrics.Total(Instrument));
        Assert.Equal(2 * (Retries + 1), RefusingConsumer.Attempts(provider));
    }

    [Fact]
    public async Task Tags_the_dead_letter_with_the_queue_it_was_parked_on_and_its_type()
    {
        var telemetry = TestHarness.CreateTelemetry();
        using var metrics = new MetricCollector(telemetry.Meter);

        await using var provider = BuildBus<RefusingConsumer>(telemetry);
        var harness = await StartAsync(provider);

        await harness.Bus.Publish(new PoisonMessage(Guid.CreateVersion7()));
        await WaitForAsync(() => metrics.Total(Instrument) >= 1);

        var measurement = Assert.Single(metrics.For(Instrument));
        Assert.True(measurement.Tagged("message_type", nameof(PoisonMessage)));
        Assert.True(measurement.Tagged("queue", "refusing"));
    }

    [Fact]
    public async Task Leaves_the_counter_alone_when_a_retry_rescues_the_message()
    {
        var telemetry = TestHarness.CreateTelemetry();
        using var metrics = new MetricCollector(telemetry.Meter);

        await using var provider = BuildBus<FlakyConsumer>(telemetry);
        var harness = await StartAsync(provider);

        await harness.Bus.Publish(new PoisonMessage(Guid.CreateVersion7()));

        Assert.True(await harness.Consumed.Any<PoisonMessage>(context => context.Exception is null));

        Assert.Equal(0, metrics.Total(Instrument));
    }

    [Fact]
    public async Task Logs_the_give_up_as_an_error_so_it_reaches_the_alerting_sink()
    {
        var logger = new RecordingLogger<DeadLetterObserver>();
        var observer = new DeadLetterObserver(TestHarness.CreateTelemetry(), logger);

        var context = Substitute.For<ConsumeContext<PoisonMessage>>();
        context.ReceiveContext.InputAddress.Returns(new Uri("loopback://localhost/takeauction-broadcast-bid-placed"));

        await observer.ConsumeFault(context, TimeSpan.Zero, "BroadcastBidPlacedConsumer", new InvalidOperationException());

        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Error && entry.Message.Contains("takeauction-broadcast-bid-placed_error"));
    }

    private static ServiceProvider BuildBus<TConsumer>(TakeAuctionTelemetry telemetry)
        where TConsumer : class, IConsumer<PoisonMessage> =>
        new ServiceCollection()
            .AddSingleton(telemetry)
            .AddSingleton<ILogger<DeadLetterObserver>>(NullLogger<DeadLetterObserver>.Instance)
            .AddSingleton<AttemptLedger>()
            .AddReceiveObserver<DeadLetterObserver>()
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddConsumer<TConsumer>().Endpoint(endpoint => endpoint.Name = "refusing");
                bus.UsingInMemory((context, transport) =>
                {
                    transport.UseMessageRetry(retry => retry.Immediate(Retries));
                    transport.ConfigureEndpoints(context);
                });
            })
            .BuildServiceProvider(true);

    private static async Task<ITestHarness> StartAsync(IServiceProvider provider)
    {
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        return harness;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }

    public sealed record PoisonMessage(Guid Id);

    public sealed class AttemptLedger
    {
        private int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);

        public int Record() => Interlocked.Increment(ref _attempts);
    }

    public sealed class RefusingConsumer(AttemptLedger ledger) : IConsumer<PoisonMessage>
    {
        public static int Attempts(IServiceProvider provider) =>
            provider.GetRequiredService<AttemptLedger>().Attempts;

        public Task Consume(ConsumeContext<PoisonMessage> context)
        {
            ledger.Record();

            throw new InvalidOperationException("The consumer refuses every message.");
        }
    }

    public sealed class FlakyConsumer(AttemptLedger ledger) : IConsumer<PoisonMessage>
    {
        public Task Consume(ConsumeContext<PoisonMessage> context) =>
            ledger.Record() == 1
                ? throw new InvalidOperationException("The first attempt fails, the retry succeeds.")
                : Task.CompletedTask;
    }
}
