using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Command.Model;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Workflow;

public sealed class PortfolioWorkflowTests
{
    static readonly DateTime Now = new(2026, 8, 30, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Gate", "PF-11")]
    [Trait("Category", "Portfolio")]
    public void Resolution_is_deterministic_defensive_and_hash_complete()
    {
        var x = Catalog("Weekly", "FuturesOptions", "VerticalSpread", 202);
        var resolver = new PortfolioFundStrategyResolver();

        var first = resolver.Resolve(x.WorkflowId, 3, Guid.NewGuid(), x.Portfolio, x.Policy, [x.Fund], [x.Allocation], [x.Envelope], [x.Assignment], 2026, "Weekly", "ES", "FuturesOptions", Now);
        var second = resolver.Resolve(x.WorkflowId, 3, first.CorrelationId, x.Portfolio, x.Policy, [x.Fund], [x.Allocation], [x.Envelope], [x.Assignment], 2026, "Weekly", "ES", "FuturesOptions", Now);

        first.PayloadSha256.Should().HaveLength(64).And.Be(second.PayloadSha256);
        first.Assignments.Should().ContainSingle();
        first.ValidUntilUtc.Should().Be(x.Envelope.ExpiresAtUtc);
        first.Assignments[0].Should().NotBeSameAs(x.Assignment);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("ambiguous")]
    [InlineData("blocked")]
    [InlineData("expired")]
    [Trait("Gate", "PF-11")]
    [Trait("Category", "Portfolio")]
    public void Resolution_fails_closed_for_invalid_catalogs(string defect)
    {
        var x = Catalog("Daily", "Futures", "DirectionalFuture", 201);
        var funds = defect == "missing" ? Array.Empty<FundMandateReadModel>() : defect == "ambiguous" ? new[] { x.Fund, x.Fund with { FundId = 999 } } : [x.Fund];
        var envelope = defect == "blocked" ? x.Envelope with { CapacityState = FundCapacityState.Blocked }
            : defect == "expired" ? x.Envelope with { ExpiresAtUtc = Now } : x.Envelope;

        var action = () => new PortfolioFundStrategyResolver().Resolve(x.WorkflowId, 1, Guid.NewGuid(), x.Portfolio, x.Policy, funds, [x.Allocation], [envelope], [x.Assignment], 2026, "Daily", "ES", "Futures", Now);

        action.Should().Throw<PortfolioResolutionException>();
    }

    [Fact]
    [Trait("Gate", "PF-12")]
    [Trait("Category", "Portfolio")]
    public void Reservation_is_idempotent_and_rejects_key_reuse_with_changed_payload()
    {
        var (aggregate, request, snapshot) = Reservation("VerticalSpread", 2);
        var first = aggregate.Reserve(request, snapshot, 7001, [8001, 8002], Now, "operator");
        var replay = aggregate.Reserve(request, snapshot, 9999, [9998, 9999], Now.AddSeconds(1), "operator");

        first.Disposition.Should().Be(ReservationDisposition.Committed);
        replay.Disposition.Should().Be(ReservationDisposition.IdempotentReplay);
        replay.Order.OrderId.Should().Be(7001);
        replay.Trades.Select(x => x.TradeId).Should().Equal(8001, 8002);

        var changed = request with { UnderlyingRoot = "NQ" };
        var action = () => aggregate.Reserve(changed, snapshot, 7002, [8003, 8004], Now, "operator");
        action.Should().Throw<InvalidOperationException>().WithMessage("*IdempotencyKeyConflict*");
    }

    [Fact]
    [Trait("Gate", "PF-28")]
    [Trait("Category", "Portfolio")]
    public void Manual_order_draft_uses_canonical_authority_without_trade_or_execution_side_effects()
    {
        var key = Guid.NewGuid();
        var request = new CreateManualFundOrderRequest
        {
            PortfolioId = 101, PortfolioVersion = 4, FundId = 202, FundMandateVersion = 3,
            UnderlyingRoot = "ES", RequestedTradeDate = DateOnly.FromDateTime(Now),
            RequestedMaturityDate = DateOnly.FromDateTime(Now.AddMonths(1)), Reference = "operator draft",
            IdempotencyKey = key, RequestedAtUtc = Now, ExpiresAtUtc = Now.AddDays(1),
        };
        var aggregate = new PortfolioFundCompositionAggregate();

        var first = aggregate.CreateManualDraft(request, 7001, Now, "operator");
        var replay = aggregate.CreateManualDraft(request, 9999, Now.AddMinutes(1), "operator");

        first.Order.OrderId.Should().Be(7001);
        first.Order.Origin.Should().Be(CompositionOrigin.ManualUi);
        first.Order.Status.Should().Be(FundCompositionState.Draft.ToString());
        first.Trades.Should().BeEmpty();
        replay.Disposition.Should().Be(ReservationDisposition.IdempotentReplay);
        aggregate.Orders.Should().ContainSingle();
        Enum.TryParse<FundCompositionState>(first.Order.Status, out var state).Should().BeTrue();
        new[] { FundCompositionState.ExecutionRequested, FundCompositionState.Executing, FundCompositionState.Executed }
            .Should().NotContain(state);
    }

    [Fact]
    [Trait("Gate", "PF-14")]
    [Trait("Gate", "PF-15")]
    [Trait("Category", "Portfolio")]
    public void Composition_and_risk_references_are_immutable_and_stop_before_execution()
    {
        var (aggregate, request, snapshot) = Reservation("DirectionalFuture", 1);
        var reserved = aggregate.Reserve(request, snapshot, 7001, [8001], Now, "operator");
        var composing = aggregate.MarkComposing(7001, reserved.AggregateVersion);
        var candidateHash = new string('a', 64);
        var riskHash = new string('b', 64);

        var pending = aggregate.RecordComposed(7001, composing.AggregateVersion, new OrderCompositionResultReference
        {
            ResultId = Guid.NewGuid(), ResultSha256 = candidateHash, InvocationId = Guid.NewGuid(),
            EvaluatedAtUtc = Now.AddSeconds(1), ExpiresAtUtc = Now.AddMinutes(5),
        }, Now.AddSeconds(2));
        var approved = aggregate.RecordRiskOutcome(7001, pending.AggregateVersion, new RiskManagementResultReference
        {
            ResultId = Guid.NewGuid(), ResultSha256 = riskHash, Decision = RiskDecision.Approved,
            EvaluatedAtUtc = Now.AddSeconds(3), ExpiresAtUtc = Now.AddMinutes(5),
            EnvelopeId = snapshot.RiskEnvelope.EnvelopeId, EnvelopeVersion = snapshot.RiskEnvelope.EnvelopeVersion,
            CandidateSha256 = candidateHash,
        }, Now.AddSeconds(4));

        approved.Status.Should().Be(nameof(FundCompositionState.RiskApproved));
        approved.OrderId.Should().Be(7001);
        approved.RiskResultHash.Should().Be(riskHash);
        var transition = () => aggregate.MarkComposing(7001, approved.AggregateVersion);
        transition.Should().Throw<InvalidOperationException>();
        typeof(PortfolioFundCompositionAggregate).GetMethods().Select(x => x.Name)
            .Should().NotContain(x => x.Contains("Execute", StringComparison.OrdinalIgnoreCase) || x.Contains("Broker", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Gate", "PF-14")]
    [Trait("Gate", "PF-15")]
    [Trait("Category", "Portfolio")]
    public void Stale_mismatched_and_duplicate_different_results_fail_closed()
    {
        var (aggregate, request, snapshot) = Reservation("DirectionalFuture", 1);
        var reserved = aggregate.Reserve(request, snapshot, 7001, [8001], Now, "operator");
        var composing = aggregate.MarkComposing(7001, reserved.AggregateVersion);
        var expired = new OrderCompositionResultReference
        {
            ResultId = Guid.NewGuid(), ResultSha256 = new string('a', 64), InvocationId = Guid.NewGuid(),
            EvaluatedAtUtc = Now, ExpiresAtUtc = Now.AddSeconds(1),
        };
        var stale = () => aggregate.RecordComposed(7001, composing.AggregateVersion, expired, Now.AddSeconds(1));
        stale.Should().Throw<InvalidOperationException>();

        var valid = expired with { ExpiresAtUtc = Now.AddMinutes(5) };
        var pending = aggregate.RecordComposed(7001, composing.AggregateVersion, valid, Now.AddSeconds(1));
        var mismatch = () => aggregate.RecordRiskOutcome(7001, pending.AggregateVersion, new RiskManagementResultReference
        {
            ResultId = Guid.NewGuid(), ResultSha256 = new string('b', 64), Decision = RiskDecision.Rejected,
            EvaluatedAtUtc = Now, ExpiresAtUtc = Now.AddMinutes(5), EnvelopeId = snapshot.RiskEnvelope.EnvelopeId,
            EnvelopeVersion = 1, CandidateSha256 = new string('c', 64),
        }, Now.AddSeconds(2));
        mismatch.Should().Throw<InvalidOperationException>().WithMessage("*candidate hash*");
    }

    [Fact]
    [Trait("Gate", "PF-12")]
    [Trait("Gate", "PF-14")]
    [Trait("Gate", "PF-15")]
    [Trait("Category", "Portfolio")]
    public void Composition_events_and_snapshots_retain_exact_ids_and_terminal_references()
    {
        var (_, request, snapshot) = Reservation("DirectionalFuture", 1);
        var aggregate = new PortfolioFundAggregate();
        aggregate.RestoreSnapshot(new PortfolioFundAggregateSnapshot(
            1, snapshot.Fund, snapshot.Assignments, [], [Guid.NewGuid()]));

        var reserved = aggregate.ReserveComposition(Guid.NewGuid(), 1, request, snapshot, 7001, [8001], Now, "operator");
        var composing = aggregate.MarkCompositionComposing(Guid.NewGuid(), 2, 7001, 1, Now.AddSeconds(1), "operator");
        var candidateHash = new string('a', 64);
        var composed = aggregate.RecordCompositionResult(Guid.NewGuid(), 3, 7001, 2, new OrderCompositionResultReference
        {
            ResultId = Guid.NewGuid(), ResultSha256 = candidateHash, InvocationId = Guid.NewGuid(),
            EvaluatedAtUtc = Now.AddSeconds(1), ExpiresAtUtc = Now.AddMinutes(5),
        }, Now.AddSeconds(2), "operator");
        var riskId = Guid.NewGuid();
        var riskHash = new string('b', 64);
        var risk = aggregate.RecordRiskResult(Guid.NewGuid(), 4, 7001, 3, new RiskManagementResultReference
        {
            ResultId = riskId, ResultSha256 = riskHash, Decision = RiskDecision.Approved,
            EvaluatedAtUtc = Now.AddSeconds(2), ExpiresAtUtc = Now.AddMinutes(5),
            EnvelopeId = snapshot.RiskEnvelope.EnvelopeId, EnvelopeVersion = snapshot.RiskEnvelope.EnvelopeVersion,
            CandidateSha256 = candidateHash,
        }, Now.AddSeconds(3), "operator");

        var replay = new PortfolioFundAggregate();
        replay.RestoreSnapshot(new PortfolioFundAggregateSnapshot(1, snapshot.Fund, snapshot.Assignments, [], [Guid.NewGuid()]));
        replay.Replay([(PortfolioFundDomainEvent)reserved, composing, composed, risk]);
        var restored = new PortfolioFundAggregate();
        restored.RestoreSnapshot(replay.CaptureSnapshot());

        restored.Revision.Should().Be(5);
        restored.Composition(7001).Order.Should().BeEquivalentTo(replay.Composition(7001).Order);
        restored.Composition(7001).Order.Status.Should().Be(nameof(FundCompositionState.RiskApproved));
        restored.Composition(7001).Order.RiskResultId.Should().Be(riskId);
        restored.Composition(7001).Trades.Select(x => x.TradeId).Should().Equal(8001);
    }

    internal static (PortfolioFundCompositionAggregate Aggregate, ReserveFundOrderCompositionRequest Request, PortfolioFundStrategySnapshot Snapshot) Reservation(string family, int count)
    {
        var x = Catalog(count == 4 ? "Monthly" : count == 2 ? "Weekly" : "Daily", count == 1 ? "Futures" : "FuturesOptions", family, 200 + count);
        var snapshot = new PortfolioFundStrategyResolver().Resolve(x.WorkflowId, 1, Guid.NewGuid(), x.Portfolio, x.Policy, [x.Fund], [x.Allocation], [x.Envelope], [x.Assignment], 2026, x.Fund.DecisionHorizon, "ES", x.Assignment.AssetType, Now);
        var instructions = Enumerable.Range(1, count).Select(i => new TradeInstruction
        {
            TradeFamily = family, TradeRole = i == 1 ? "Primary" : "Related", DirectionOrBias = "Bullish",
            TradeAction = i % 2 == 0 ? "Sell" : "Buy", IsPrimaryTrade = i == 1, UnderlyingRoot = "ES",
            RequestedTradeDate = DateOnly.FromDateTime(Now), RequestedMaturityDate = DateOnly.FromDateTime(Now.AddDays(30)),
            Reference = $"leg-{i}", CreatedOnUtc = Now, CreatedBy = "test",
        }).ToArray();
        return (new PortfolioFundCompositionAggregate(), new ReserveFundOrderCompositionRequest
        {
            WorkflowId = x.WorkflowId, WorkflowRevision = 1, TradeSelectionInvocationId = Guid.NewGuid(),
            TradeSelectionResultId = Guid.NewGuid(), TradeSelectionResultSha256 = new string('1', 64),
            PortfolioId = x.Portfolio.PortfolioId, PortfolioVersion = x.Portfolio.PortfolioVersion,
            FundId = x.Fund.FundId, FundMandateVersion = x.Fund.FundMandateVersion,
            TradeTemplateId = x.Assignment.TradeTemplateId, TradeTemplateVersion = x.Assignment.TradeTemplateVersion,
            OrderCompositionProfileId = x.Assignment.OrderCompositionProfileId, OrderCompositionProfileVersion = x.Assignment.OrderCompositionProfileVersion,
            UnderlyingRoot = "ES", DecisionHorizon = x.Fund.DecisionHorizon,
            RequestedTradeDate = DateOnly.FromDateTime(Now), RequestedMaturityDate = DateOnly.FromDateTime(Now.AddDays(30)),
            TradeInstructions = instructions, Origin = CompositionOrigin.StrategyWorkflow, IdempotencyKey = Guid.NewGuid(),
            RequestedAtUtc = Now, ExpiresAtUtc = Now.AddMinutes(10), PortfolioFundStrategySnapshotSha256 = snapshot.PayloadSha256,
        }, snapshot);
    }

    internal static CatalogData Catalog(string horizon, string asset, string family, int fundId)
    {
        var portfolio = new PortfolioReadModel
        {
            PortfolioId = 101, Name = "Core", PortfolioVersion = 2, BaseCurrency = "USD",
            OperatingState = PortfolioOperatingState.Active, EffectiveFromUtc = Now.AddDays(-10), ActivePolicyId = 9001,
            ActivePolicyVersion = 4, CreatedOnUtc = Now.AddDays(-10), CreatedBy = "admin",
        };
        var policy = new PortfolioFinancialPolicyReadModel
        {
            PortfolioId = 101, PolicyId = 9001, PolicyVersion = 4, Name = "Core limits", OperatingState = PortfolioFinancialPolicyState.Active,
            BaseCurrency = "USD", CapitalBase = 1_000_000m, MaximumDeployableCapital = 900_000m, MaximumRiskPerTrade = 10_000m,
            MaximumAggregateRisk = 100_000m, MaximumMargin = 500_000m, MaximumGrossNotional = 5_000_000m, MaximumOpenPositions = 100,
            MaximumDrawdownAmount = 200_000m, TradeFamilyLimits = [new() { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 10_000m, MaximumAggregateRisk = 100_000m, MaximumMargin = 500_000m, MaximumGrossNotional = 5_000_000m, MaximumOpenPositions = 100 }],
            EffectiveFromUtc = Now.AddDays(-10), CreatedOnUtc = Now.AddDays(-10), CreatedBy = "admin",
        };
        var fund = new FundMandateReadModel
        {
            PortfolioId = 101, FundId = fundId, FundCode = $"{horizon}-ES", Name = horizon, FundMandateVersion = 3,
            TradingYear = 2026, OperatingState = FundOperatingState.Active, EffectiveFromUtc = Now.AddDays(-5),
            DecisionHorizon = horizon, Objective = "ES", UnderlyingUniverse = ["ES"], EligibleAssetTypes = [asset],
            PermittedTradeFamilies = [family], CreatedOnUtc = Now.AddDays(-5), CreatedBy = "admin",
        };
        var allocation = new FundAllocationReadModel
        {
            PortfolioId = 101, PortfolioVersion = 2, FundId = fundId, FundMandateVersion = 3, AllocationVersion = 1,
            TargetWeight = .3m, MaximumWeight = .5m, AllocatedCapital = 100000m, Currency = "USD",
            EffectiveFromUtc = Now.AddDays(-2), SourcePolicyId = 9001, SourcePolicyVersion = 4, CreatedOnUtc = Now.AddDays(-2), CreatedBy = "admin",
        };
        var envelope = new FundRiskEnvelopeReadModel
        {
            PortfolioId = 101, PortfolioVersion = 2, FundId = fundId, FundMandateVersion = 3,
            EnvelopeId = Guid.Parse("22222222-2222-2222-2222-222222222222"), EnvelopeVersion = 1,
            CapacityState = FundCapacityState.Available, Currency = "USD", AllocatedCapital = 100000m, AvailableCapital = 80000m,
            MaximumRiskPerTrade = 1000m, MaximumAggregateRisk = 5000m, MaximumMargin = 20000m, MaximumGrossNotional = 200000m,
            MaximumContracts = 10, MaximumOpenPositions = 5, RemainingLossBudget = 10000m,
            EffectiveFromUtc = Now.AddDays(-1), ExpiresAtUtc = Now.AddDays(1), SourcePolicyId = portfolio.ActivePolicyId,
            SourcePolicyVersion = 4, CreatedOnUtc = Now.AddDays(-1), CreatedBy = "admin",
        };
        var assignment = new FundTradeTemplateAssignmentReadModel
        {
            PortfolioId = 101, PortfolioVersion = 2, FundId = fundId, FundMandateVersion = 3, AssignmentVersion = 1,
            TradeTemplateId = Guid.NewGuid(), TradeTemplateVersion = 2, Enabled = true, DecisionHorizon = horizon,
            UnderlyingUniverse = ["ES"], AssetType = asset, TradeFamily = family, Priority = 1, EffectiveFromUtc = Now.AddDays(-1),
            TradeSelectionHintProfileId = Guid.NewGuid(), TradeSelectionHintProfileVersion = 1,
            OrderCompositionProfileId = Guid.NewGuid(), OrderCompositionProfileVersion = 1,
            CreatedOnUtc = Now.AddDays(-1), CreatedBy = "admin",
        };
        return new(Guid.NewGuid(), portfolio, policy, fund, allocation, envelope, assignment);
    }

    internal sealed record CatalogData(Guid WorkflowId, PortfolioReadModel Portfolio, PortfolioFinancialPolicyReadModel Policy, FundMandateReadModel Fund,
        FundAllocationReadModel Allocation, FundRiskEnvelopeReadModel Envelope, FundTradeTemplateAssignmentReadModel Assignment);
}
