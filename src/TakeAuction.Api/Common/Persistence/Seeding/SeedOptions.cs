namespace TakeAuction.Api.Common.Persistence.Seeding;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; init; } = true;

    public bool SeedAuctions { get; init; } = true;

    public string? DefaultPassword { get; init; }

    public string AdminEmail { get; init; } = "admin@takeauction.local";

    public string SellerEmail { get; init; } = "seller@takeauction.local";

    public string BidderEmail { get; init; } = "bidder@takeauction.local";
}
