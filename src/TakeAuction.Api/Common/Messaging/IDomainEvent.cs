using MediatR;

namespace TakeAuction.Api.Common.Messaging;

public interface IDomainEvent : INotification
{
    DateTimeOffset OccurredAtUtc { get; }
}
