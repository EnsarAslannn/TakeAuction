using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Common.Messaging.Outbox;

public sealed class IntegrationEventTypeRegistryTests
{
    private readonly IntegrationEventTypeRegistry _registry = TestHarness.CreateIntegrationEventTypeRegistry();

    [Theory]
    [InlineData(typeof(BidPlacedIntegrationEvent))]
    [InlineData(typeof(AuctionCreatedIntegrationEvent))]
    [InlineData(typeof(AuctionEndedIntegrationEvent))]
    public void Resolves_every_contract_that_can_be_queued(Type contract)
    {
        Assert.Equal(contract, _registry.Resolve(IntegrationEventTypeRegistry.NameOf(contract)));
    }

    [Fact]
    public void Refuses_a_name_no_contract_answers_to()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _registry.Resolve("GoneAwayEvent"));

        Assert.Contains("GoneAwayEvent", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_a_contract_by_its_type_name_so_moving_the_namespace_does_not_orphan_stored_rows()
    {
        Assert.Equal("BidPlacedIntegrationEvent", IntegrationEventTypeRegistry.NameOf(typeof(BidPlacedIntegrationEvent)));
    }
}
