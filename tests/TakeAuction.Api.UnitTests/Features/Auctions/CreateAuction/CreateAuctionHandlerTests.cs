using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions.CreateAuction;

public sealed class CreateAuctionHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext = TestHarness.CreateDbContext();
    private readonly FixedTimeProvider _timeProvider = new(TestHarness.Now);
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly CreateAuctionHandler _handler;

    public CreateAuctionHandlerTests() =>
        _handler = new CreateAuctionHandler(
            _dbContext,
            _timeProvider,
            _publisher,
            NullLogger<CreateAuctionHandler>.Instance);

    [Fact]
    public async Task Persists_the_auction()
    {
        var command = Command();

        var response = await _handler.Handle(command, CancellationToken.None);

        var persisted = await _dbContext.Auctions.SingleAsync();
        Assert.Equal(response.Id, persisted.Id);
        Assert.Equal(command.SellerId, persisted.SellerId);
        Assert.Equal(command.Title, persisted.Title);
        Assert.Equal(command.Description, persisted.Description);
        Assert.Equal(command.StartingPrice, persisted.StartingPrice);
        Assert.Equal(command.MinimumBidIncrement, persisted.MinimumBidIncrement);
    }

    [Fact]
    public async Task Seeds_current_price_from_the_starting_price()
    {
        await _handler.Handle(Command() with { StartingPrice = 250.50m }, CancellationToken.None);

        var persisted = await _dbContext.Auctions.SingleAsync();
        Assert.Equal(250.50m, persisted.CurrentPrice);
    }

    [Fact]
    public async Task Trims_title_and_description()
    {
        await _handler.Handle(
            Command() with { Title = "  Spaced title  ", Description = "  Spaced description  " },
            CancellationToken.None);

        var persisted = await _dbContext.Auctions.SingleAsync();
        Assert.Equal("Spaced title", persisted.Title);
        Assert.Equal("Spaced description", persisted.Description);
    }

    [Fact]
    public async Task Marks_a_future_auction_as_scheduled()
    {
        var response = await _handler.Handle(
            Command() with { StartsAtUtc = TestHarness.Now.AddHours(4), EndsAtUtc = TestHarness.Now.AddDays(2) },
            CancellationToken.None);

        Assert.Equal(nameof(AuctionStatus.Scheduled), response.Status);
    }

    [Fact]
    public async Task Marks_an_immediately_starting_auction_as_active()
    {
        var response = await _handler.Handle(
            Command() with { StartsAtUtc = TestHarness.Now, EndsAtUtc = TestHarness.Now.AddDays(2) },
            CancellationToken.None);

        Assert.Equal(nameof(AuctionStatus.Active), response.Status);
    }

    [Fact]
    public async Task Stamps_creation_time_from_the_time_provider()
    {
        var response = await _handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(TestHarness.Now, response.CreatedAtUtc);
    }

    [Fact]
    public async Task Publishes_the_auction_created_event()
    {
        var command = Command();

        var response = await _handler.Handle(command, CancellationToken.None);

        await _publisher.Received(1).Publish(
            Arg.Is<AuctionCreatedEvent>(domainEvent =>
                domainEvent.AuctionId == response.Id
                && domainEvent.SellerId == command.SellerId
                && domainEvent.StartingPrice == command.StartingPrice
                && domainEvent.OccurredAtUtc == TestHarness.Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_an_end_date_that_precedes_the_start_date()
    {
        var command = Command() with
        {
            StartsAtUtc = TestHarness.Now.AddDays(2),
            EndsAtUtc = TestHarness.Now.AddDays(1)
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Empty(await _dbContext.Auctions.ToListAsync());
    }

    public void Dispose() => _dbContext.Dispose();

    private static CreateAuctionCommand Command() => new(
        Guid.CreateVersion7(),
        "Vintage mechanical watch",
        "A fully serviced 1968 mechanical watch with original box and papers.",
        1500.00m,
        25.00m,
        TestHarness.Now.AddHours(1),
        TestHarness.Now.AddDays(3));
}
