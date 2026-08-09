using System.Reflection;
using FluentValidation;
using MediatR;

namespace TakeAuction.Api.Common.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddTakeAuctionMessaging(this IServiceCollection services, Assembly assembly)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
