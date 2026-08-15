using System.Reflection;

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

        var options = section.Get<OutboxOptions>() ?? new OutboxOptions();

        if (options.DispatcherEnabled)
        {
            services.AddHostedService<OutboxDispatcherService>();
        }

        return services;
    }
}
