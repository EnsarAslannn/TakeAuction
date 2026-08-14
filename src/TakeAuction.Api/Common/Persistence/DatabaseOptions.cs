namespace TakeAuction.Api.Common.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool MigrateOnStartup { get; init; }
}
