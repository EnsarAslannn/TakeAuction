using TakeAuction.Api.Features.Notifications.GetNotifications;

namespace TakeAuction.Api.UnitTests.Features.Notifications;

public sealed class GetNotificationsQueryTests
{
    [Theory]
    [InlineData(0, GetNotificationsQuery.DefaultTake)]
    [InlineData(-5, GetNotificationsQuery.DefaultTake)]
    [InlineData(7, 7)]
    [InlineData(GetNotificationsQuery.MaxTake, GetNotificationsQuery.MaxTake)]
    [InlineData(100_000, GetNotificationsQuery.MaxTake)]
    public void Keeps_a_read_within_bounds(int requested, int expected) =>
        Assert.Equal(expected, new GetNotificationsQuery(Guid.CreateVersion7(), requested).NormalizedTake);
}
