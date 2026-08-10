namespace TakeAuction.Api.Common.Messaging;

public sealed class MessageBrokerOptions
{
    public const string SectionName = "MessageBroker";

    public string? ConnectionString { get; set; }

    public string EndpointPrefix { get; set; } = "takeauction";

    public ushort PrefetchCount { get; set; } = 16;

    public int RetryCount { get; set; } = 3;

    public int RetryIntervalSeconds { get; set; } = 2;
}
