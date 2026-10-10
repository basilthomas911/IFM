using MessagePack;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class OptionChainSubscriptionCoverageTests
{
    [Fact]
    public void Buffered_scope_covers_small_price_moves_and_both_wings()
    {
        var definitions = Enumerable.Range(0, 41).SelectMany(i => new[] { Contract("C", 4900 + i * 5), Contract("P", 4900 + i * 5) }).ToArray();
        var first = OptionChainStrikeWindow.Select(definitions, 5000, 10, 2.5);
        var buffer = OptionChainSubscriptionCoverage.Buffer(definitions, first, 50);
        var identities = buffer.ToDictionary(x => x.ContractId, x => (x.MappingVersion!, x.DefinitionDigest!));
        Assert.True(OptionChainSubscriptionCoverage.Covers(identities, OptionChainStrikeWindow.Select(definitions, 5005, 10, 2.5).Contracts));
        Assert.Contains(buffer, x => x.StrikePrice == 4925);
        Assert.Contains(buffer, x => x.StrikePrice == 5075);
        Assert.False(OptionChainSubscriptionCoverage.Covers(identities, OptionChainStrikeWindow.Select(definitions, 5100, 10, 2.5).Contracts));
    }
    [Fact]
    public void Mapping_or_digest_change_prevents_reuse()
    {
        var contract = Contract("C", 5000);
        var identities = new Dictionary<string, (string, string)> { [contract.ContractId] = ("v1", "d1") };
        Assert.True(OptionChainSubscriptionCoverage.Covers(identities, [contract]));
        Assert.False(OptionChainSubscriptionCoverage.Covers(identities, [contract with { MappingVersion = "v2" }]));
        Assert.False(OptionChainSubscriptionCoverage.Covers(identities, [contract with { DefinitionDigest = "d2" }]));
    }
    [Fact]
    public void Buffer_does_not_clip_requested_contracts_to_worker_limit()
    {
        var definitions = Enumerable.Range(0, 2200).Select(i => Contract("C", 4000 + i)).ToArray();
        var window = OptionChainStrikeWindow.Select(definitions, 5000, 200, 2.5, [definitions[^1].ContractId]);
        var buffered = OptionChainSubscriptionCoverage.Buffer(definitions, window, 2000);
        Assert.Equal(window.Contracts, buffered);
    }
    [Fact]
    public void Ownership_and_wing_width_round_trip_without_changing_existing_keys()
    {
        var query = new GetEvaluatedOptionChainQuery { SubscriptionOwnerId = "view-1", SpreadWingWidth = 50, RequiredContractIds = ["wing"] };
        var result = MessagePackSerializer.Deserialize<GetEvaluatedOptionChainQuery>(MessagePackSerializer.Serialize(query));
        Assert.Equal("view-1", result.SubscriptionOwnerId);
        Assert.Equal(50, result.SpreadWingWidth);
        Assert.Equal(query.RequiredContractIds, result.RequiredContractIds);
    }
    [Fact]
    public void Unavailable_quote_diagnostics_round_trip_without_restoring_a_price()
    {
        var observed = DateTimeOffset.UtcNow.AddSeconds(-3);
        var row = new EvaluatedOptionContractReadModel("wing", 5000, true, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, false, true, null, null, null,
            QuoteUnavailableReason: "QuoteAgeExceeded", LastQuoteEventAtUtc: observed, LastQuoteReceivedAtUtc: observed);
        var copy = MessagePackSerializer.Deserialize<EvaluatedOptionContractReadModel>(MessagePackSerializer.Serialize(row));
        Assert.Equal("QuoteAgeExceeded", copy.QuoteUnavailableReason);
        Assert.Equal(observed, copy.LastQuoteEventAtUtc);
        Assert.Null(copy.Bid);
        Assert.False(copy.SelectionValid);
        Assert.True(copy.IsStale);
    }
    static FuturesOptionContractReadModel Contract(string right, double strike) => new(
        $"{right}{strike}", "", "ES", "", "OPT", "USD", "CME", "50", new(2026, 12, 1), strike, right)
        { MappingVersion = "v1", DefinitionDigest = "d1" };
}
