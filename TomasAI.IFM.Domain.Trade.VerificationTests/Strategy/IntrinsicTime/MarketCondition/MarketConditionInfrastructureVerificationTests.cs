using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.IntegratedTests;
using TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.MarketCondition;

[CollectionDefinition(IntrinsicTimeStrategyWorkflowRuntimeCollection.Name, DisableParallelization = true)]
public sealed class MarketConditionInfrastructureVerificationCollection;

/// <summary>
/// Runs the reviewable Market Condition qualification scenarios through the deployed actor/storage topology.
/// The shared scenario implementation prevents the Integration and Verification evidence from drifting.
/// </summary>
[Trait("Category", "Verification")]
[Collection(IntrinsicTimeStrategyWorkflowRuntimeCollection.Name)]
public sealed class MarketConditionInfrastructureVerificationTests(
    WebApplicationFactory<Program> sourceFactory,
    TradeDatabaseFixture database)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<TradeDatabaseFixture>
{
    IntrinsicTimeStrategyWorkflowRuntimeIntegrationTests Scenarios() => new(sourceFactory, database);

    [Fact]
    public Task Daily_weekly_monthly_success_crosschecks_function_projection_workflow_query_and_restart() =>
        Scenarios().Projected_regime_completion_advances_each_workflow_to_market_condition_once();

    [Fact]
    public async Task Production_snapshot_aggregation_reads_each_authority_once_and_seals_one_market_moment()
    {
        var input = MarketConditionVerificationScenario.Healthy();
        var command = new ExecuteMarketConditionPipelineCommand
        {
            CommandId = Guid.NewGuid(), EntityId = new(input.WorkflowView.EntityId, input.WorkflowView.WorkflowId),
            InputWorkflowRevision = input.InputWorkflowRevision, WorkflowView = input.WorkflowView,
            TriggerEvent = input.TriggerEvent, RequestedAtUtc = input.Snapshot.EvaluationTimestampUtc,
            ExpiresAtUtc = input.Snapshot.EvaluationTimestampUtc.AddSeconds(5), ParameterSet = input.ParameterSet,
            ParameterPayloadSha256 = MarketConditionParameterPayload.ComputeSha256(input.ParameterSet),
            TargetHorizon = input.ParameterSet.TargetHorizon, FundId = input.ParameterSet.FundId,
            InstrumentRoot = input.ParameterSet.InstrumentRoot
        };
        var sources = new SnapshotAuthorityFixture(input.Snapshot);
        var coordinator = new MarketConditionSnapshotAdapterCoordinator(
            sources, sources, sources, sources, sources, sources);

        var snapshot = await coordinator.PublishAsync(command, input.Snapshot.WorkflowEligibility,
            input.Snapshot.EvaluationTimestampUtc);

        sources.Calls.Values.Should().OnlyContain(value => value == 1);
        sources.Calls.Should().HaveCount(6);
        snapshot.SnapshotSha256.Should().Be(MarketConditionSnapshotHash.Compute(snapshot));
        snapshot.OptionChainQuality.CandidateContractCount.Should().Be(24);
        snapshot.DataQualityItems.Should().HaveCount(10);
    }

    [Fact]
    public async Task No_trade_timeout_projection_and_lost_notification_faults_are_terminal_and_non_continuing()
    {
        await Scenarios().Market_condition_no_trade_is_projected_terminal_and_never_dispatches_trade_selection();
        await Scenarios().Market_condition_timeout_is_terminal_unprojected_and_never_dispatches_trade_selection();
        await Scenarios().Market_condition_projector_exception_fails_workflow_without_projection_state_or_continuation();
        await Scenarios().Market_condition_persistence_exception_leaves_observable_orphan_and_never_continues();
        await Scenarios().Market_condition_matching_retry_survives_host_restart_without_recapture_or_redispatch();
    }

    sealed class SnapshotAuthorityFixture(MarketConditionSnapshot source) :
        IMarketConditionFuturesQuoteAdapter, IMarketConditionOptionUniverseAdapter,
        IMarketConditionSessionAdapter, IMarketConditionEventRiskAdapter,
        IMarketConditionVolatilityAdapter, IMarketConditionOperationalHealthAdapter
    {
        public Dictionary<string, int> Calls { get; } = [];
        void Called(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

        public ValueTask<MarketConditionRawFuturesQuote> ReadOnceAsync(string instrumentRoot,
            DateTime evaluationTimestampUtc, CancellationToken cancellationToken)
        {
            Called("futures");
            var value = source.FuturesQuote;
            return ValueTask.FromResult(new MarketConditionRawFuturesQuote
            {
                BidPrice = (double)value.BidPrice, AskPrice = (double)value.AskPrice,
                BidSize = (double)value.BidSize, AskSize = (double)value.AskSize,
                LastPrice = (double)value.LastPrice, OneMinuteMoveAtr = (double)value.OneMinuteMoveAtr,
                QuoteObservation = value.QuoteObservation, TradeObservation = value.TradeObservation
            });
        }

        public ValueTask<IReadOnlyCollection<MarketConditionRawOptionContract>> ReadOnceAsync(
            string instrumentRoot, decimal futuresUnderlyingPrice,
            MarketConditionOptionLiquidityConfiguration configuration, DateTime evaluationTimestampUtc,
            CancellationToken cancellationToken)
        {
            Called("options");
            IReadOnlyCollection<MarketConditionRawOptionContract> values = Enumerable.Range(0, 24).Select(index =>
                new MarketConditionRawOptionContract
                {
                    ContractId = $"ES-V-{index:00}", InstrumentRoot = "ES",
                    ExpirationDate = DateOnly.FromDateTime(evaluationTimestampUtc).AddDays(index < 12 ? 7 : 14),
                    OptionType = index % 2 == 0 ? "Call" : "Put", StrikePrice = (double)futuresUnderlyingPrice,
                    BidPrice = 1d, AskPrice = 1.1d, BidSize = 5d, AskSize = 5d,
                    UnderlyingPrice = (double)futuresUnderlyingPrice,
                    Observation = source.OptionChainQuality.Observation
                }).ToArray();
            return ValueTask.FromResult(values);
        }

        public ValueTask<MarketConditionSessionState> ReadOnceAsync(string instrumentRoot,
            MarketConditionSessionConfiguration configuration, DateTime evaluationTimestampUtc,
            CancellationToken cancellationToken)
        { Called("session"); return ValueTask.FromResult(source.SessionState); }

        public ValueTask<MarketConditionEventRiskState> ReadOnceAsync(
            MarketConditionEventRiskConfiguration configuration, DateTime evaluationTimestampUtc,
            CancellationToken cancellationToken)
        { Called("event"); return ValueTask.FromResult(source.EventRiskState); }

        ValueTask<MarketConditionVolatilityShockState> IMarketConditionVolatilityAdapter.ReadOnceAsync(
            string instrumentRoot, DateTime evaluationTimestampUtc, CancellationToken cancellationToken)
        { Called("volatility"); return ValueTask.FromResult(source.VolatilityShockState); }

        public ValueTask<IReadOnlyCollection<MarketConditionOperationalHealthItem>> ReadOnceAsync(
            IReadOnlyCollection<string> requiredSources, DateTime evaluationTimestampUtc,
            CancellationToken cancellationToken)
        {
            Called("health");
            return ValueTask.FromResult<IReadOnlyCollection<MarketConditionOperationalHealthItem>>(
                source.OperationalHealth);
        }
    }
}
