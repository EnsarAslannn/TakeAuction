namespace TakeAuction.Api.Common.RealTime;

public sealed class RealTimeOptions
{
    public const string SectionName = "RealTime";

    public string? RedisConnectionString { get; set; }

    public string ChannelPrefix { get; set; } = "takeauction:signalr";

    public int KeepAliveSeconds { get; set; } = 15;

    public int ClientTimeoutSeconds { get; set; } = 30;
}
