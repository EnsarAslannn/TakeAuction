using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TakeAuction.Api.Features.Chat;

public static class ChatFeatureExtensions
{
    public static IServiceCollection AddChatFeature(this IServiceCollection services)
    {
        services.AddSingleton<IChatKnowledgeBase, StaticChatKnowledgeBase>();
        services.TryAddScoped<IChatAuctionContextReader, ChatAuctionContextReader>();
        services.AddScoped<IChatService, KnowledgeChatService>();
        return services;
    }

    public static IEndpointRouteBuilder MapUnversionedChatEndpoint(this IEndpointRouteBuilder builder)
    {
        PostChatEndpoint.Map(builder, "/api/chat", "PostChat");
        return builder;
    }
}
