using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Workflow;

public sealed class PortfolioCompositionScenarios
{
    static readonly DateTime Now = new(2026, 8, 30, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Gate", "PF-11")]
    [Trait("Category", "Portfolio")]
    public void Given_daily_weekly_monthly_funds_each_horizon_resolves_without_guessing()
    {
        var resolver = new PortfolioFundStrategyResolver();
        foreach (var (horizon, asset, family, fundId) in new[]
        {
            ("Daily", "Futures", "DirectionalFuture", 201),
            ("Weekly", "FuturesOptions", "VerticalSpread", 202),
            ("Monthly", "FuturesOptions", "IronCondor", 203),
        })
        {
            var x = Catalog(horizon, asset, family, fundId);
            var result = resolver.Resolve(Guid.NewGuid(), 1, Guid.NewGuid(), x.Portfolio, x.Policy, [x.Fund], [x.Allocation], [x.Envelope], [x.Assignment], 2026, horizon, "ES", asset, Now);
            result.Fund.FundId.Should().Be(fundId);
            result.Assignments.Should().ContainSingle(x => x.TradeFamily == family);
        }
    }

    [Fact]
    [Trait("Gate", "PF-12")]
    [Trait("Gate", "PF-13")]
    [Trait("Category", "Portfolio")]
    public void Given_an_accepted_selected_result_when_reserved_then_stable_integer_ids_are_returned_once()
    {
        var (aggregate, request, snapshot) = Reservation();
        var first = aggregate.Reserve(request, snapshot, 9001, [9101, 9102], Now, "workflow");
        var retry = aggregate.Reserve(request, snapshot, 9999, [9991, 9992], Now.AddSeconds(1), "workflow");

        first.Order.OrderId.Should().Be(9001);
        first.Trades.Select(x => x.TradeId).Should().Equal(9101, 9102);
        retry.Order.OrderId.Should().Be(9001);
        retry.Disposition.Should().Be(ReservationDisposition.IdempotentReplay);
    }

    [Fact]
    [Trait("Gate", "PF-28")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Given_a_manual_operator_request_when_created_then_it_is_a_canonical_non_executable_draft()
    {
        var request = new CreateManualFundOrderRequest
        {
            PortfolioId = 101, PortfolioVersion = 2, FundId = 202, FundMandateVersion = 3,
            UnderlyingRoot = "ES", RequestedTradeDate = DateOnly.FromDateTime(Now),
            RequestedMaturityDate = DateOnly.FromDateTime(Now.AddMonths(1)), Reference = "manual review",
            IdempotencyKey = Guid.NewGuid(), RequestedAtUtc = Now, ExpiresAtUtc = Now.AddDays(1),
        };

        var result = new PortfolioFundCompositionAggregate().CreateManualDraft(request, 9001, Now, "operator");

        result.Order.Origin.Should().Be(CompositionOrigin.ManualUi);
        result.Order.Status.Should().Be(FundCompositionState.Draft.ToString());
        result.Trades.Should().BeEmpty();
        result.Order.Status.Should().NotContain("Execut");
    }

    [Theory]
    [InlineData(RiskDecision.Approved, FundCompositionState.RiskApproved)]
    [InlineData(RiskDecision.Rejected, FundCompositionState.RiskRejected)]
    [Trait("Gate", "PF-14")]
    [Trait("Gate", "PF-15")]
    [Trait("Category", "Portfolio")]
    public void Given_a_valid_candidate_when_risk_decides_then_only_the_reference_is_recorded(RiskDecision decision, FundCompositionState expected)
    {
        var (aggregate, request, snapshot) = Reservation();
        var reserved = aggregate.Reserve(request, snapshot, 9001, [9101, 9102], Now, "workflow");
        var composing = aggregate.MarkComposing(9001, reserved.AggregateVersion);
        var candidate = new string('c', 64);
        var pending = aggregate.RecordComposed(9001, composing.AggregateVersion, new()
        {
            ResultId = Guid.NewGuid(), ResultSha256 = candidate, InvocationId = Guid.NewGuid(), EvaluatedAtUtc = Now, ExpiresAtUtc = Now.AddMinutes(5),
        }, Now.AddSeconds(1));
        var final = aggregate.RecordRiskOutcome(9001, pending.AggregateVersion, new()
        {
            ResultId = Guid.NewGuid(), ResultSha256 = new string('r', 64), Decision = decision, EvaluatedAtUtc = Now,
            ExpiresAtUtc = Now.AddMinutes(5), EnvelopeId = snapshot.RiskEnvelope.EnvelopeId,
            EnvelopeVersion = snapshot.RiskEnvelope.EnvelopeVersion, CandidateSha256 = candidate,
        }, Now.AddSeconds(2));

        final.Status.Should().Be(expected.ToString());
        final.Status.Contains("Execut", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    static (PortfolioFundCompositionAggregate, ReserveFundOrderCompositionRequest, PortfolioFundStrategySnapshot) Reservation()
    {
        var x = Catalog("Weekly", "FuturesOptions", "VerticalSpread", 202);
        var snapshot = new PortfolioFundStrategyResolver().Resolve(Guid.NewGuid(), 1, Guid.NewGuid(), x.Portfolio, x.Policy, [x.Fund], [x.Allocation], [x.Envelope], [x.Assignment], 2026, "Weekly", "ES", "FuturesOptions", Now);
        var request = new ReserveFundOrderCompositionRequest
        {
            WorkflowId = snapshot.WorkflowId, WorkflowRevision = 1, TradeSelectionInvocationId = Guid.NewGuid(), TradeSelectionResultId = Guid.NewGuid(),
            TradeSelectionResultSha256 = new string('s', 64), PortfolioId = 101, PortfolioVersion = 2, FundId = 202, FundMandateVersion = 3,
            TradeTemplateId = x.Assignment.TradeTemplateId, TradeTemplateVersion = 1, OrderCompositionProfileId = x.Assignment.OrderCompositionProfileId,
            OrderCompositionProfileVersion = 1, UnderlyingRoot = "ES", DecisionHorizon = "Weekly", RequestedTradeDate = DateOnly.FromDateTime(Now),
            TradeInstructions = [Instruction(1, true), Instruction(2, false)], Origin = CompositionOrigin.StrategyWorkflow,
            IdempotencyKey = Guid.NewGuid(), RequestedAtUtc = Now, ExpiresAtUtc = Now.AddMinutes(5), PortfolioFundStrategySnapshotSha256 = snapshot.PayloadSha256,
        };
        return (new(), request, snapshot);
    }

    static TradeInstruction Instruction(int ordinal, bool primary) => new()
    {
        TradeFamily = "VerticalSpread", TradeRole = primary ? "Primary" : "Related", DirectionOrBias = "Bullish", TradeAction = ordinal == 1 ? "Buy" : "Sell",
        IsPrimaryTrade = primary, UnderlyingRoot = "ES", RequestedTradeDate = DateOnly.FromDateTime(Now), Reference = $"leg-{ordinal}", CreatedOnUtc = Now, CreatedBy = "workflow",
    };

    static CatalogData Catalog(string horizon, string asset, string family, int fundId)
    {
        var portfolio = new PortfolioReadModel { PortfolioId = 101, Name = "Core", PortfolioVersion = 2, OperatingState = PortfolioOperatingState.Active, EffectiveFromUtc = Now.AddDays(-2), ActivePolicyId = 9001, ActivePolicyVersion = 1, CreatedOnUtc = Now.AddDays(-2), CreatedBy = "admin" };
        var policy = new PortfolioFinancialPolicyReadModel { PortfolioId = 101, PolicyId = 9001, PolicyVersion = 1, Name = "Limits", OperatingState = PortfolioFinancialPolicyState.Active, CapitalBase = 1_000_000m, MaximumDeployableCapital = 900_000m, MaximumRiskPerTrade = 10_000m, MaximumAggregateRisk = 100_000m, MaximumMargin = 500_000m, MaximumGrossNotional = 5_000_000m, MaximumOpenPositions = 100, MaximumDrawdownAmount = 200_000m, TradeFamilyLimits = [new() { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 10_000m, MaximumAggregateRisk = 100_000m, MaximumMargin = 500_000m, MaximumGrossNotional = 5_000_000m, MaximumOpenPositions = 100 }], EffectiveFromUtc = Now.AddDays(-2), CreatedOnUtc = Now.AddDays(-2), CreatedBy = "admin" };
        var fund = new FundMandateReadModel { PortfolioId = 101, FundId = fundId, FundCode = horizon, Name = horizon, FundMandateVersion = 3, TradingYear = 2026, OperatingState = FundOperatingState.Active, EffectiveFromUtc = Now.AddDays(-1), DecisionHorizon = horizon, Objective = "ES", UnderlyingUniverse = ["ES"], EligibleAssetTypes = [asset], PermittedTradeFamilies = [family], CreatedOnUtc = Now.AddDays(-1), CreatedBy = "admin" };
        var allocation = new FundAllocationReadModel { PortfolioId = 101, PortfolioVersion = 2, FundId = fundId, FundMandateVersion = 3, AllocationVersion = 1, TargetWeight = .2m, MaximumWeight = .4m, AllocatedCapital = 100000m, EffectiveFromUtc = Now.AddDays(-1), SourcePolicyId = 9001, SourcePolicyVersion = 1, CreatedOnUtc = Now.AddDays(-1), CreatedBy = "admin" };
        var envelope = new FundRiskEnvelopeReadModel { PortfolioId = 101, PortfolioVersion = 2, FundId = fundId, FundMandateVersion = 3, EnvelopeId = Guid.NewGuid(), EnvelopeVersion = 1, CapacityState = FundCapacityState.Available, AllocatedCapital = 100000m, AvailableCapital = 80000m, MaximumRiskPerTrade = 1000m, MaximumAggregateRisk = 5000m, MaximumMargin = 20000m, MaximumGrossNotional = 200000m, MaximumContracts = 10, MaximumOpenPositions = 5, RemainingLossBudget = 10000m, EffectiveFromUtc = Now.AddHours(-1), ExpiresAtUtc = Now.AddDays(1), SourcePolicyId = portfolio.ActivePolicyId, SourcePolicyVersion = 1, CreatedOnUtc = Now.AddHours(-1), CreatedBy = "admin" };
        var assignment = new FundTradeTemplateAssignmentReadModel { PortfolioId = 101, PortfolioVersion = 2, FundId = fundId, FundMandateVersion = 3, AssignmentVersion = 1, TradeTemplateId = Guid.NewGuid(), TradeTemplateVersion = 1, Enabled = true, DecisionHorizon = horizon, UnderlyingUniverse = ["ES"], AssetType = asset, TradeFamily = family, Priority = 1, EffectiveFromUtc = Now.AddHours(-1), TradeSelectionHintProfileId = Guid.NewGuid(), TradeSelectionHintProfileVersion = 1, OrderCompositionProfileId = Guid.NewGuid(), OrderCompositionProfileVersion = 1, CreatedOnUtc = Now.AddHours(-1), CreatedBy = "admin" };
        return new(portfolio, policy, fund, allocation, envelope, assignment);
    }

    sealed record CatalogData(PortfolioReadModel Portfolio, PortfolioFinancialPolicyReadModel Policy, FundMandateReadModel Fund, FundAllocationReadModel Allocation, FundRiskEnvelopeReadModel Envelope, FundTradeTemplateAssignmentReadModel Assignment);
}
