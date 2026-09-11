using MediatR;

namespace TakeAuction.Api.Features.Notifications.MarkNotificationsRead;

public sealed record MarkNotificationsReadCommand(Guid UserId) : IRequest<int>;
