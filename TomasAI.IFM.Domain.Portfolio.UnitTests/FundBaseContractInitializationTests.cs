using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.ReferencePreparation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests;

public sealed class FundBaseContractInitializationTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);
    static readonly TradeStrategySymbolReadModel Product = new() { Symbol = "MES", Currency = "USD", Exchange = "XCME", Description = "Micro E-mini S&P 500 Futures" };
    static readonly FundBaseContractRule Rule = new() { Symbol = "MES", ForwardCycles = 2 };

    static InstrumentDefinitionSelection Definition(int year, int month, int day) => new()
    {
        SnapshotId = Guid.NewGuid(), Dataset = "GLBX.MDP3", Root = "MES", InstrumentClass = "F",
        InstrumentId = (uint)(year * 10000 + month * 100 + day), PublisherId = 1, Currency = "USD", Exchange = "XCME",
        RawSymbol = $"MES{year}{month}", ExpirationUtc = new(year, month, day, 14, 30, 0, TimeSpan.Zero),
        Multiplier = 5, TickSize = .25m, DefinitionTimestampUtc = Now, DefinitionDigest = "verified", RawDefinitionReference = "provider-row"
    };

    [Fact]
    public void New_MES_is_initialized_with_provider_economics_and_current_plus_two_cycles()
    {
        var definitions = new[] { Definition(2027, 9, 17), Definition(2026, 9, 18), Definition(2027, 3, 19), Definition(2026, 12, 18), Definition(2027, 6, 17) };
        var planned = FundBaseContractPreparation.Plan(Rule, Product, definitions, [], DateOnly.FromDateTime(Now.UtcDateTime), Now);
        planned.Select(x => x.ContractId).Should().Equal("MES20261218", "MES20270319", "MES20270617");
        planned.Count(x => x.OnTheRun && x.Rollover).Should().Be(1);
        planned[0].MultiplierValue.Should().Be(5);
        planned[0].TickSize.Should().Be(.25m);
        foreach (var contract in planned)
        {
            contract.SchemaVersion.Should().Be(1);
            contract.ReviewState.Should().Be(ReferenceReviewState.Draft);
            ReferencePayloadCodec.ReadFuture(ReferencePayloadCodec.Write(contract), contract).Should().Be(contract);
        }
        planned.Should().OnlyContain(x => x.Dataset == "GLBX.MDP3" && x.DefinitionDigest == "verified");
        FundBaseContractQueryService.Resolve(Rule, planned, definitions, DateOnly.FromDateTime(Now.UtcDateTime))
            .Should().BeEquivalentTo(planned, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Existing_current_selection_and_economics_are_preserved()
    {
        var definitions = new[] { Definition(2026, 12, 18), Definition(2027, 3, 19), Definition(2027, 6, 17), Definition(2027, 9, 17) };
        var original = FundBaseContractPreparation.Plan(Rule, Product, definitions, [], DateOnly.FromDateTime(Now.UtcDateTime), Now);
        var current = original[1] with { OnTheRun = true, Rollover = true, Description = "Reviewed label", Multiplier = "5.00" };
        var planned = FundBaseContractPreparation.Plan(Rule, Product, definitions, [current], DateOnly.FromDateTime(Now.UtcDateTime), Now);
        planned[0].Should().Be(current);
        planned.Select(x => x.LastTradeDate).Should().Equal(new DateOnly(2027, 3, 19), new DateOnly(2027, 6, 17), new DateOnly(2027, 9, 17));
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("option")]
    [InlineData("missing multiplier")]
    [InlineData("missing evidence")]
    [InlineData("not active")]
    public void Ineligible_definition_prevents_partial_initialization(string failure)
    {
        var last = Definition(2027, 6, 17);
        last = failure switch
        {
            "deleted" => last with { Deleted = true }, "option" => last with { InstrumentClass = "C" },
            "missing multiplier" => last with { Multiplier = null }, "not active" => last with { ActivationUtc = Now.AddDays(1) },
            _ => last with { RawDefinitionReference = "" }
        };
        Action plan = () => FundBaseContractPreparation.Plan(Rule, Product, [Definition(2026, 12, 18), Definition(2027, 3, 19), last], [], DateOnly.FromDateTime(Now.UtcDateTime), Now);
        plan.Should().Throw<InvalidOperationException>().WithMessage("*2 qualified contracts*3 are required*");
    }

    [Fact]
    public void Existing_contracts_with_reviewed_economics_do_not_require_reinitialization_of_legacy_selector_fields()
    {
        var definitions = new[] { Definition(2026, 12, 18), Definition(2027, 3, 19), Definition(2027, 6, 17) };
        var existing = FundBaseContractPreparation.Plan(Rule, Product, definitions, [], DateOnly.FromDateTime(Now.UtcDateTime), Now);
        var legacy = definitions.Select(x => x with { Multiplier = null });
        FundBaseContractPreparation.Plan(Rule, Product, legacy, existing, DateOnly.FromDateTime(Now.UtcDateTime), Now).Should().Equal(existing);
    }

    [Fact]
    public void Equities_cannot_initialize_futures_contracts()
    {
        Action plan = () => FundBaseContractPreparation.Plan(Rule with { BaseAsset = "Equities" }, Product, [], [], DateOnly.FromDateTime(Now.UtcDateTime), Now);
        plan.Should().Throw<ArgumentException>();
    }
}
