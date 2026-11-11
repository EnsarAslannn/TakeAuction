using System.Reflection;
using MediatR;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Notifications;

namespace TakeAuction.Api.Common.Messaging.Outbox;

public static class OutboxExtensions
{
    public static IServiceCollection AddTakeAuctionOutbox(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly assembly)
    {
        var section = configuration.GetSection(OutboxOptions.SectionName);

        services.AddOptions<OutboxOptions>().Bind(section);

        services.AddSingleton(new IntegrationEventTypeRegistry(assembly));
        services.AddSingleton<OutboxSignal>();
        services.AddScoped<OutboxSignalInterceptor>();
        services.AddScoped<IOutbox, Outbox>();
        services.AddScoped<OutboxDispatcher>();
        services.AddScoped<OutboxBacklogProbe>();

        var options = section.Get<OutboxOptions>() ?? new OutboxOptions();
        if (configuration.GetValue<bool>("DurableJobs:Enabled"))
        {
            services.AddScoped<IOutboxTransport, InlineOutboxTransport>();
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(InlineOutboxBehavior<,>));
            services.AddScoped<BroadcastAuctionCreatedConsumer>();
            services.AddScoped<BroadcastAuctionOpenedConsumer>();
            services.AddScoped<BroadcastAuctionCancelledConsumer>();
            services.AddScoped<BroadcastAuctionEndedConsumer>();
            services.AddScoped<BroadcastBidPlacedConsumer>();
            services.AddScoped<NotifyAuctionOutcomeConsumer>();
            services.AddScoped<NotifyWatcherClosingSoonConsumer>();
            return services;
        }

        services.AddScoped<IOutboxTransport, BrokerOutboxTransport>();

        if (options.DispatcherEnabled)
        {
            services.AddHostedService<OutboxDispatcherService>();
        }

        if (options.BacklogSampleIntervalSeconds > 0)
        {
            services.AddSingleton<OutboxBacklogSampler>();
            services.AddHostedService(provider => provider.GetRequiredService<OutboxBacklogSampler>());
        }

        return services;
    }
}
