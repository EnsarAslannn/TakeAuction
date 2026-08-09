using System.Reflection;

namespace TakeAuction.Api.Common.Api;

public static class EndpointExtensions
{
    public static IServiceCollection AddTakeAuctionEndpoints(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.DefinedTypes.Where(IsEndpoint))
        {
            services.Add(ServiceDescriptor.Transient(typeof(IEndpoint), type));
        }

        return services;
    }

    public static IEndpointRouteBuilder MapTakeAuctionEndpoints(this IEndpointRouteBuilder builder)
    {
        using var scope = builder.ServiceProvider.CreateScope();

        foreach (var endpoint in scope.ServiceProvider.GetServices<IEndpoint>())
        {
            endpoint.MapEndpoint(builder);
        }

        return builder;
    }

    private static bool IsEndpoint(TypeInfo type) =>
        type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
        && type.IsAssignableTo(typeof(IEndpoint));
}
