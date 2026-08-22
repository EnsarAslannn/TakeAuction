using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Media;
using TakeAuction.Api.Features.Media.PurgeOrphanImages;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Media.PurgeOrphanImages;

public sealed class PurgeOrphanImagesJobTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"takeauction-purge-{Guid.CreateVersion7():n}");
    private readonly MediaOptions _options = new() { OrphanRetentionHours = 24 };
    private readonly AppDbContext _dbContext = TestHarness.CreateDbContext();
    private readonly FixedTimeProvider _time = new(TestHarness.Now);
    private readonly MediaStorage _storage;

    public PurgeOrphanImagesJobTests()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(_root);

        _storage = new MediaStorage(environment, Options.Create(_options));
        _storage.EnsureCreated();
    }

    [Fact]
    public async Task An_upload_no_auction_claims_is_swept_once_it_is_old_enough()
    {
        var orphan = WriteImage(agedHours: 48);

        var removed = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(1, removed);
        Assert.False(File.Exists(orphan));
    }

    [Fact]
    public async Task An_upload_still_inside_the_retention_window_is_left_alone()
    {
        var pending = WriteImage(agedHours: 1);

        var removed = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(0, removed);
        Assert.True(File.Exists(pending));
    }

    [Fact]
    public async Task An_upload_an_auction_points_at_survives_however_old_it_is()
    {
        var claimed = WriteImage(agedHours: 24 * 365);

        await AddAuctionAsync(_storage.UrlFor(Path.GetFileName(claimed)));

        var removed = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(0, removed);
        Assert.True(File.Exists(claimed));
    }

    [Fact]
    public async Task A_sweep_over_an_empty_folder_is_a_no_op()
    {
        Assert.Equal(0, await CreateJob().RunAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_sweep_keeps_what_is_claimed_and_drops_the_rest()
    {
        var claimed = WriteImage(agedHours: 48);
        var orphan = WriteImage(agedHours: 48);
        var fresh = WriteImage(agedHours: 2);

        await AddAuctionAsync(_storage.UrlFor(Path.GetFileName(claimed)));

        var removed = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(1, removed);
        Assert.True(File.Exists(claimed));
        Assert.False(File.Exists(orphan));
        Assert.True(File.Exists(fresh));
    }

    public void Dispose()
    {
        _dbContext.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private PurgeOrphanImagesJob CreateJob() =>
        new(
            _dbContext,
            _storage,
            Options.Create(_options),
            _time,
            NullLogger<PurgeOrphanImagesJob>.Instance);

    private string WriteImage(int agedHours)
    {
        var path = Path.Combine(_storage.ImageRoot, $"{Guid.CreateVersion7():n}.png");

        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47]);
        File.SetLastWriteTimeUtc(path, _time.GetUtcNow().AddHours(-agedHours).UtcDateTime);

        return path;
    }

    private async Task AddAuctionAsync(string imageUrl)
    {
        var auction = Auction.Create(
            Guid.CreateVersion7(),
            "A lot with a picture",
            "A detailed description of the lot on offer.",
            100m,
            5m,
            TestHarness.Now,
            TestHarness.Now.AddDays(2),
            TestHarness.Now,
            imageUrl);

        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();
    }
}
