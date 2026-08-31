using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.UnitTests.Queries;

public sealed class TradeStrategyFamilyContractTests
{
    [Fact]
    [Trait("Gate", "PF-22")]
    public void V1_seed_is_exact_broad_read_only_catalog_without_directional_variants()
    {
        TradeStrategyFamilySeed.Definitions.Should().Equal(
            ("FUTURES", "Futures"),
            ("VERTICAL_SPREAD", "Vertical Spread"),
            ("IRON_CONDOR", "Iron Condor"));
        TradeStrategyFamilySeed.Definitions.Select(x => x.SystemKey)
            .Should().NotContain(x => x.Contains("LONG") || x.Contains("SHORT") || x.Contains("BULL") || x.Contains("BEAR"));
    }

    [Fact]
    [Trait("Gate", "PF-22")]
    public void Exact_integer_identity_version_and_audit_round_trip()
    {
        var row = new TradeStrategyFamilyReadModel
        {
            TradeStrategyFamilyId = 71, DefinitionVersion = 1, SystemKey = "FUTURES", Name = "Futures",
            State = TradeStrategyFamilyState.Active, CreatedOnUtc = new DateTime(2026, 8, 30, 18, 0, 0, DateTimeKind.Utc),
            CreatedBy = "ReferenceBootstrap"
        };

        var copy = MessagePackSerializer.Deserialize<TradeStrategyFamilyReadModel>(MessagePackSerializer.Serialize(row));

        copy.Should().BeEquivalentTo(row);
        copy.Validate().Should().BeEmpty();
        TradeStrategyFamilySeed.Validate([row, row with { TradeStrategyFamilyId = 72, SystemKey = "VERTICAL_SPREAD", Name = "Vertical Spread" }, row with { TradeStrategyFamilyId = 73, SystemKey = "IRON_CONDOR", Name = "Iron Condor" }]);
    }
}
