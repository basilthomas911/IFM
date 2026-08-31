using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Contracts;

public sealed class PortfolioTransportContractTests
{
    [Fact]
    [Trait("Gate", "PF-01")]
    [Trait("Category", "Portfolio")]
    public void Nats_binary_serializer_contract_retains_all_integer_identity_components()
    {
        var serializer = new MessagePackBinarySerializer();
        var source = new PortfolioFundOrderTradeId(101, 205, 3001, 4001);

        var bytes = serializer.Serialize(source);
        var copy = serializer.Deserialize<PortfolioFundOrderTradeId>(bytes!);

        copy.Should().Be(source);
        copy!.Format().Should().Be("101.205.3001.4001");
    }

    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Category", "Portfolio")]
    public void Draft_deletion_payload_round_trips_expected_revision_and_audit_reason()
    {
        var serializer = new MessagePackBinarySerializer();
        var source = new DeleteDraftPortfolioPayload(17, "duplicate operator draft");

        var copy = serializer.Deserialize<DeleteDraftPortfolioPayload>(serializer.Serialize(source)!);

        copy.Should().Be(source);
    }
}
