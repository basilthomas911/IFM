using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using Composer = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
public sealed class CompositionEdgeTests
{
    [Theory]
    [InlineData("generation")] [InlineData("future")] [InlineData("stale")] [InlineData("crossed")]
    [InlineData("style")] [InlineData("underlying")] [InlineData("multiplier")] [InlineData("duplicate")]
    public async Task Invalid_required_market_data_fails_instead_of_selecting_from_reduced_scope(string change)
    {
        var c = await CompositionFixture.Command("BullCallDebit"); var items = c.MarketSnapshot.Instruments;
        var i = items[0].Instrument;
        i = change switch
        {
            "generation" => i with { Quote = i.Quote with { GenerationId = Guid.NewGuid() } },
            "future" => i with { Quote = i.Quote with { EventAtUtc = new DateTimeOffset(c.EvaluatedAtUtc.AddTicks(1)) } },
            "stale" => i with { Quote = i.Quote with { EventAtUtc = new DateTimeOffset(c.EvaluatedAtUtc.AddSeconds(-2)) } },
            "crossed" => i with { Quote = i.Quote with { Ask = i.Quote.Bid / 2 } },
            "style" => i with { Pricing = i.Pricing! with { Contract = i.Pricing.Contract with { ExerciseStyle = OptionExerciseStyle.American } } },
            "underlying" => i with { Underlying = i.Underlying! with { ContractId = "OTHER" } },
            "multiplier" => i with { Pricing = i.Pricing! with { Contract = i.Pricing.Contract with { Multiplier = 5 } } },
            _ => items[1].Instrument
        };
        c = c with { MarketSnapshot = c.MarketSnapshot with { Instruments = items.SetItem(0, items[0] with { Instrument = i }) } };
        Assert.Throws<CompositionException>(() => new Composer(new Black76ComposerPricer()).Calculate(c));
    }
    [Fact]
    public async Task Zero_participation_capacity_is_an_economic_rejection_and_future_risk_is_unbounded()
    {
        var c = await CompositionFixture.Command(); var model = new Composer(new Black76ComposerPricer());
        var candidate = model.Calculate(c).Candidate!;
        Assert.Equal("Unbounded", candidate.RiskEvidence.RiskBound); Assert.Null(candidate.RiskEvidence.MaximumLoss);
        Assert.True(candidate.RiskEvidence.StressLoss > candidate.RiskEvidence.PlannedLoss);
        var item = c.MarketSnapshot.Instruments[0];
        c = c with { MarketSnapshot = c.MarketSnapshot with { Instruments = [item with { Instrument = item.Instrument with { Quote = item.Instrument.Quote with { BidSize = 1, AskSize = 1 } } }] } };
        var result = model.Calculate(c); Assert.Equal(CompositionOutcome.NoCandidate, result.Outcome);
        Assert.Contains(result.CandidateDiagnostics, x => x.ReasonCode == "OC.CANDIDATE.LIQUIDITY");
    }
    [Fact]
    public async Task Exact_quote_age_boundary_is_valid_and_one_tick_older_is_stale()
    {
        var c = await CompositionFixture.Command(); var item = c.MarketSnapshot.Instruments[0];
        var old = item with { Instrument = item.Instrument with { Quote = item.Instrument.Quote with { EventAtUtc = new DateTimeOffset(c.EvaluatedAtUtc.AddMilliseconds(-1000)) } } };
        var model = new Composer(new Black76ComposerPricer());
        // Market validation succeeds at the inclusive boundary, but no positive candidate lifetime remains.
        Assert.Equal(CompositionOutcome.NoCandidate, model.Calculate(c with { MarketSnapshot = c.MarketSnapshot with { Instruments = [old] } }).Outcome);
        old = old with { Instrument = old.Instrument with { Quote = old.Instrument.Quote with { EventAtUtc = old.Instrument.Quote.EventAtUtc.AddTicks(-1) } } };
        Assert.Throws<CompositionException>(() => model.Calculate(c with { MarketSnapshot = c.MarketSnapshot with { Instruments = [old] } }));
    }
    [Fact]
    public async Task Roll_buffer_selects_the_next_contract_without_renaming_or_manufacturing_it()
    {
        var c = await CompositionFixture.Command(); var first = c.MarketSnapshot.Instruments[0];
        var old = first with { Instrument = first.Instrument with { FutureDefinition = first.Instrument.FutureDefinition! with { LastTradingUtc = new DateTimeOffset(c.EvaluatedAtUtc.AddHours(119)) } } };
        var next = first with { Instrument = first.Instrument with { ContractId = "ESH7", Quote = first.Instrument.Quote with { ContractId = "ESH7" },
            FutureDefinition = first.Instrument.FutureDefinition! with { ContractId = "ESH7", LastTradingUtc = new DateTimeOffset(c.EvaluatedAtUtc.AddDays(200)) } } };
        var r = new Composer(new Black76ComposerPricer()).Calculate(c with { MarketSnapshot = c.MarketSnapshot with { Instruments = [old, next] } });
        Assert.Equal("ESH7", r.Candidate!.Legs[0].InstrumentId);
    }
    [Fact]
    public async Task Pricing_failure_is_terminal_even_when_other_contracts_could_form_a_candidate()
    {
        var c = await CompositionFixture.Command("ShortBalancedIronCondor");
        var pricer = new BrokenPricer();
        Assert.Throws<CompositionException>(() => new Composer(pricer).Calculate(c)); Assert.Equal(1, pricer.Calls);
    }
    [Theory]
    [InlineData("en-US")] [InlineData("fr-FR")] [InlineData("tr-TR")]
    public async Task Result_and_candidate_hashes_are_culture_independent(string name)
    {
        var c = await CompositionFixture.Command("BearPutDebit"); var model = new Composer(new Black76ComposerPricer());
        var expected = CompositionHash.Compute(model.Calculate(c)); var saved = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name); Assert.Equal(expected, CompositionHash.Compute(model.Calculate(c))); }
        finally { CultureInfo.CurrentCulture = saved; }
        Assert.Equal(CompositionHash.Compute(new { Price = 1.00m }), CompositionHash.Compute(new { Price = 1m }));
    }
    [Fact]
    public async Task Strict_catalog_schema_supports_depth_eight_and_rejects_unknown_or_missing_parameters()
    {
        var c = await CompositionFixture.Command(); var rules = c.CompositionBinding.Rules;
        var predicate = new CompositionPredicate { Comparison = CompositionComparison.Greater, Feature = CompositionFeature.SelectionConfidence, Values = [.5m], Required = true };
        for (int n = 1; n < 8; n++) predicate = new() { Comparison = CompositionComparison.All, Children = [predicate] };
        var rule = rules.VariantRules[0] with
        {
            HardBounds = [new() { Parameter = CompositionParameter.FeePerContract, Minimum = 0, Maximum = 10, Grid = .1m }],
            AdjustmentRules = [new() { Code = "Fee", Parameter = CompositionParameter.FeePerContract, Operation = CompositionOperation.Add, Operand = .1m, Predicate = predicate }]
        };
        rules = rules with { VariantRules = [rule] };
        var shape = StrategyCatalogValidation.ReadShape(CompositionRulesSchema.Settings());
        StrategyCatalogValidation.ValidateParameters(shape, JsonSerializer.SerializeToElement(rules));
        CompositionRulesContract.Read(CompositionRulesContract.Serialize(rules));
        var tooDeep = rules with { VariantRules = [rule with { AdjustmentRules = [rule.AdjustmentRules[0] with { Predicate = new() { Comparison = CompositionComparison.All, Children = [predicate] } }] }] };
        Assert.Throws<CompositionException>(() => CompositionRulesContract.Validate(tooDeep));
        var json = JsonSerializer.Serialize(rules);
        Assert.Throws<JsonException>(() => CompositionRulesContract.Read(json.Replace("\"FeePerContract\":2.5", "\"UnknownField\":2.5")));
    }
    [Fact]
    public async Task Grid_rounding_stays_on_grid_and_rejects_empty_grid_intersections()
    {
        var c = await CompositionFixture.Command(); var rule = c.CompositionBinding.Rules.VariantRules[0] with
        { HardBounds = [new() { Parameter = CompositionParameter.FeePerContract, Minimum = 2.49m, Maximum = 2.51m, Grid = .1m }] };
        Assert.Equal(2.5m, CompositionParameterResolver.Resolve(rule, new Dictionary<CompositionFeature, decimal>(), default).Values.FeePerContract);
        rule = rule with { HardBounds = [rule.HardBounds[0] with { Grid = 1 }] };
        Assert.Throws<CompositionException>(() => CompositionParameterResolver.Resolve(rule, new Dictionary<CompositionFeature, decimal>(), default));
    }
    sealed class BrokenPricer : IFuturesOptionComposerPricer
    {
        public int Calls; public string Version => new Black76ComposerPricer().Version;
        public CompositionValuation Calculate(CompositionMarketInstrument i, DateTimeOffset at) { Calls++; throw new CompositionException("OC.PRICING.IV_FAILED"); }
        public decimal Tick(CompositionMarketInstrument i, decimal premium, bool leg) => throw new InvalidOperationException("No candidate can be ranked after pricing failure.");
    }
}
