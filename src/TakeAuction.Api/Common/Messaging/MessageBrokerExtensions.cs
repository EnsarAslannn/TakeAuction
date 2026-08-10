using System.Reflection;
using MassTransit;

namespace TakeAuction.Api.Common.Messaging;

public static class MessageBrokerExtensions
{
    public static IServiceCollection AddTakeAuctionMessageBroker(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly assembly)
    {
        var section = configuration.GetSection(MessageBrokerOptions.SectionName);

        services.AddOptions<MessageBrokerOptions>().Bind(section);

        var options = section.Get<MessageBrokerOptions>() ?? new MessageBrokerOptions();
        var connectionString = options.ConnectionString
            ?? configuration.GetConnectionString("RabbitMq");

        services.AddMassTransit(bus =>
        {
            bus.SetEndpointNameFormatter(
                new KebabCaseEndpointNameFormatter(options.EndpointPrefix, includeNamespace: false));

            bus.AddConsumers(assembly);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                bus.UsingInMemory((context, transport) =>
                {
                    ConfigureRetry(transport, options);
                    transport.ConfigureEndpoints(context);
                });
            }
            else
            {
                bus.UsingRabbitMq((context, transport) =>
                {
                    transport.Host(new Uri(connectionString));
                    transport.PrefetchCount = options.PrefetchCount;

                    ConfigureRetry(transport, options);
                    transport.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }

    private static void ConfigureRetry(IBusFactoryConfigurator transport, MessageBrokerOptions options) =>
        transport.UseMessageRetry(retry =>
            retry.Interval(options.RetryCount, TimeSpan.FromSeconds(options.RetryIntervalSeconds)));
}
