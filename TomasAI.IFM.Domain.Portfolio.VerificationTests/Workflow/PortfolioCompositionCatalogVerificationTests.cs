using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Workflow;

public sealed class PortfolioCompositionCatalogVerificationTests
{
    static readonly DateTime Now = new(2026, 8, 30, 17, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Daily", "DirectionalFuture", "Bullish", 1)]
    [InlineData("Weekly", "VerticalSpread", "Bullish", 2)]
    [InlineData("Weekly", "VerticalSpread", "Bearish", 2)]
    [InlineData("Monthly", "IronCondor", "Neutral", 4)]
    [InlineData("Monthly", "IronCondor", "BullishBias", 4)]
    [Trait("Gate", "PF-11")]
    [Trait("Gate", "PF-12")]
    [Trait("Gate", "PF-13")]
    [Trait("Gate", "PF-14")]
    [Trait("Gate", "PF-15")]
    [Trait("Category", "Portfolio")]
    public void Representative_catalog_retains_snapshot_and_exact_integer_composition_ids(string horizon, string family, string bias, int tradeCount)
    {
        var template = Guid.NewGuid();
        var profile = Guid.NewGuid();
        var workflow = Guid.NewGuid();
        var snapshot = Snapshot(workflow, horizon, family, template, profile);
        var request = new ReserveFundOrderCompositionRequest
        {
            WorkflowId = workflow, WorkflowRevision = 1, TradeSelectionInvocationId = Guid.NewGuid(), TradeSelectionResultId = Guid.NewGuid(),
            TradeSelectionResultSha256 = new string('d', 64), PortfolioId = 501, PortfolioVersion = 2, FundId = 600 + tradeCount,
            FundMandateVersion = 3, TradeTemplateId = template, TradeTemplateVersion = 1, OrderCompositionProfileId = profile,
            OrderCompositionProfileVersion = 1, UnderlyingRoot = "ES", DecisionHorizon = horizon, RequestedTradeDate = DateOnly.FromDateTime(Now),
            TradeInstructions = Enumerable.Range(0, tradeCount).Select(i => new TradeInstruction
            {
                TradeFamily = family, TradeRole = i == 0 ? "Primary" : "Related", DirectionOrBias = bias,
                TradeAction = i % 2 == 0 ? "Buy" : "Sell", IsPrimaryTrade = i == 0, UnderlyingRoot = "ES",
                RequestedTradeDate = DateOnly.FromDateTime(Now), Reference = $"{family}-{i + 1}", CreatedOnUtc = Now, CreatedBy = "verification",
            }).ToArray(),
            Origin = CompositionOrigin.StrategyWorkflow, IdempotencyKey = Guid.NewGuid(), RequestedAtUtc = Now,
            ExpiresAtUtc = Now.AddMinutes(5), PortfolioFundStrategySnapshotSha256 = snapshot.PayloadSha256,
        };
        var ids = Enumerable.Range(8101, tradeCount).ToArray();

        var result = new PortfolioFundCompositionAggregate().Reserve(request, snapshot, 7101, ids, Now, "verification");

        result.Order.OrderId.Should().Be(7101);
        result.Trades.Select(x => x.TradeId).Should().Equal(ids);
        result.Trades.Should().OnlyContain(x => x.DirectionOrBias == bias && x.TradeFamily == family);
        result.Order.StrategySnapshotHash.Should().Be(snapshot.PayloadSha256);
    }

    [Fact]
    [Trait("Gate", "PF-15")]
    [Trait("Category", "Portfolio")]
    public void Portfolio_assemblies_have_no_broker_execution_or_live_trade_database_dependency()
    {
        var references = typeof(PortfolioFundCompositionAggregate).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        references.Any(x => x is not null && (x.Contains("Broker", StringComparison.OrdinalIgnoreCase) || x.Contains("OrderExecution", StringComparison.OrdinalIgnoreCase) || x.Contains("TradeDb", StringComparison.OrdinalIgnoreCase))).Should().BeFalse();
    }

    [Fact]
    [Trait("Gate", "PF-28")]
    [Trait("Category", "Portfolio")]
    public void Manual_and_workflow_origins_are_unambiguous_in_the_unified_order_projection()
    {
        var request = new CreateManualFundOrderRequest
        {
            PortfolioId = 501, PortfolioVersion = 2, FundId = 601, FundMandateVersion = 3,
            UnderlyingRoot = "ES", RequestedTradeDate = DateOnly.FromDateTime(Now),
            RequestedMaturityDate = DateOnly.FromDateTime(Now.AddMonths(1)), IdempotencyKey = Guid.NewGuid(),
            RequestedAtUtc = Now, ExpiresAtUtc = Now.AddDays(1),
        };
        var manual = new PortfolioFundCompositionAggregate().CreateManualDraft(request, 7101, Now, "verification");
        var template = Guid.NewGuid();
        var profile = Guid.NewGuid();
        var workflow = Guid.NewGuid();
        var snapshot = Snapshot(workflow, "Daily", "DirectionalFuture", template, profile);
        var automatedRequest = new ReserveFundOrderCompositionRequest
        {
            WorkflowId = workflow, WorkflowRevision = 1, TradeSelectionInvocationId = Guid.NewGuid(), TradeSelectionResultId = Guid.NewGuid(),
            TradeSelectionResultSha256 = new string('a', 64), PortfolioId = 501, PortfolioVersion = 2, FundId = 601,
            FundMandateVersion = 3, TradeTemplateId = template, TradeTemplateVersion = 1, OrderCompositionProfileId = profile,
            OrderCompositionProfileVersion = 1, UnderlyingRoot = "ES", DecisionHorizon = "Daily",
            RequestedTradeDate = DateOnly.FromDateTime(Now), TradeInstructions = [new()
            {
                TradeFamily = "DirectionalFuture", TradeRole = "Primary", DirectionOrBias = "Bullish", TradeAction = "Buy",
                IsPrimaryTrade = true, UnderlyingRoot = "ES", RequestedTradeDate = DateOnly.FromDateTime(Now), CreatedOnUtc = Now, CreatedBy = "verification",
            }], Origin = CompositionOrigin.StrategyWorkflow, IdempotencyKey = Guid.NewGuid(), RequestedAtUtc = Now,
            ExpiresAtUtc = Now.AddMinutes(5), PortfolioFundStrategySnapshotSha256 = snapshot.PayloadSha256,
        };
        var automated = new PortfolioFundCompositionAggregate().Reserve(automatedRequest, snapshot, 7102, [8101], Now, "verification");

        manual.Order.Origin.Should().Be(CompositionOrigin.ManualUi);
        automated.Order.Origin.Should().Be(CompositionOrigin.StrategyWorkflow);
        new[] { manual.Order.OrderId, automated.Order.OrderId }.Should().OnlyHaveUniqueItems();
    }

    static PortfolioFundStrategySnapshot Snapshot(Guid workflow, string horizon, string family, Guid template, Guid profile)
    {
        var fundId = 600 + (family == "DirectionalFuture" ? 1 : family == "VerticalSpread" ? 2 : 4);
        var snapshot = new PortfolioFundStrategySnapshot
        {
            WorkflowId = workflow, WorkflowRevision = 1, CorrelationId = workflow,
            Portfolio = new() { PortfolioId = 501, PortfolioVersion = 2, Name = "Verification", OperatingState = PortfolioOperatingState.Active, EffectiveFromUtc = Now.AddDays(-1), ActivePolicyId = 9001, ActivePolicyVersion = 1, CreatedOnUtc = Now.AddDays(-1), CreatedBy = "verification" },
            FinancialPolicy = new() { PortfolioId = 501, PolicyId = 9001, PolicyVersion = 1, Name = "Verification limits", OperatingState = PortfolioFinancialPolicyState.Active, CapitalBase = 1_000_000m, MaximumDeployableCapital = 900_000m, MaximumRiskPerTrade = 10_000m, MaximumAggregateRisk = 100_000m, MaximumMargin = 500_000m, MaximumGrossNotional = 5_000_000m, MaximumOpenPositions = 100, MaximumDrawdownAmount = 200_000m, TradeFamilyLimits = [new() { TradeStrategyFamilyId = family == "DirectionalFuture" ? 1 : family == "VerticalSpread" ? 2 : 3, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 10_000m, MaximumAggregateRisk = 100_000m, MaximumMargin = 500_000m, MaximumGrossNotional = 5_000_000m, MaximumOpenPositions = 100 }], EffectiveFromUtc = Now.AddDays(-1), CreatedOnUtc = Now.AddDays(-1), CreatedBy = "verification" },
            Fund = new() { PortfolioId = 501, FundId = fundId, FundMandateVersion = 3, FundCode = horizon, Name = horizon, TradingYear = 2026, OperatingState = FundOperatingState.Active, EffectiveFromUtc = Now.AddDays(-1), DecisionHorizon = horizon, Objective = "ES", UnderlyingUniverse = ["ES"], EligibleAssetTypes = [family == "DirectionalFuture" ? "Futures" : "FuturesOptions"], PermittedTradeFamilies = [family], CreatedOnUtc = Now.AddDays(-1), CreatedBy = "verification" },
            Allocation = new() { PortfolioId = 501, PortfolioVersion = 2, FundId = fundId, FundMandateVersion = 3, AllocationVersion = 1, SourcePolicyId = 9001, SourcePolicyVersion = 1 },
            RiskEnvelope = new() { PortfolioId = 501, PortfolioVersion = 2, FundId = fundId, FundMandateVersion = 3, EnvelopeId = Guid.NewGuid(), EnvelopeVersion = 1, CapacityState = FundCapacityState.Available, SourcePolicyId = 9001, SourcePolicyVersion = 1, EffectiveFromUtc = Now.AddHours(-1), ExpiresAtUtc = Now.AddHours(1) },
            Assignments = [new() { PortfolioId = 501, PortfolioVersion = 2, FundId = fundId, FundMandateVersion = 3, AssignmentVersion = 1, TradeTemplateId = template, TradeTemplateVersion = 1, Enabled = true, DecisionHorizon = horizon, UnderlyingUniverse = ["ES"], AssetType = family == "DirectionalFuture" ? "Futures" : "FuturesOptions", TradeFamily = family, EffectiveFromUtc = Now.AddHours(-1), TradeSelectionHintProfileId = Guid.NewGuid(), TradeSelectionHintProfileVersion = 1, OrderCompositionProfileId = profile, OrderCompositionProfileVersion = 1, CreatedOnUtc = Now.AddHours(-1), CreatedBy = "verification" }],
            ResolvedAtUtc = Now, ValidUntilUtc = Now.AddHours(1),
        };
        return snapshot with { PayloadSha256 = PortfolioCanonicalHash.Compute(snapshot) };
    }
}
