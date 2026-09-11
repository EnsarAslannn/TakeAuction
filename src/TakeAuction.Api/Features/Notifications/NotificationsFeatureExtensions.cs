namespace TakeAuction.Api.Features.Notifications;

public static class NotificationsFeatureExtensions
{
    public static IServiceCollection AddNotificationsFeature(this IServiceCollection services)
    {
        services.AddScoped<NotificationInbox>();

        return services;
    }
}
