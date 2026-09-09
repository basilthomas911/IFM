using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    [Theory,Trait("Category","PortfolioFinancialFiveStage"),Trait("Gate","PF-FIN-05")]
    [MemberData(nameof(FiveStageCases))]
    public async Task Five_real_calculation_actors_preserve_the_same_trigger_and_actual_upstream_results(TimeFrameType horizon,string variant)
    {
        var broker=Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL")??throw new InvalidOperationException("An isolated test broker is required.");
        ExecuteMarketConditionAssessmentCommand assessment=null!;
        await using var host=Host(services=>
        {
            services.RemoveAll<IMarketConditionAssessmentSnapshotProvider>();
            services.AddSingleton<IMarketConditionAssessmentSnapshotProvider>(new FinancialPathMarketInputs(()=>assessment));
        },broker);
        _=host.CreateClient();var supervisor=host.Services.GetRequiredService<IActorSupervisor>();var producer=host.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new(ActorType.Realtime,$"FiveStageFinancial{Guid.NewGuid():N}"));
        try
        {
            await new TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialSchema(FinancialBoundaryTransactions()).InitializeAsync();
            new RiskEvaluator().Calculate(await RiskFixture.Command(variant,horizon:horizon));
            assessment=AssessmentFixture.Command(horizon,DateTime.UtcNow,$"FIVE{Guid.NewGuid():N}");
            var balanced=variant.Contains("Balanced",StringComparison.Ordinal);
            var bearish=variant.Contains("Bear",StringComparison.Ordinal)||variant=="ShortFuture";
            var signal=assessment.TriggerEvent.FuturesItiSignal! with {
                IntrinsicPrice=balanced?100:bearish?95:105,
                IntrinsicTimeTrend=bearish?IntrinsicTimeTrendType.DownTrend:IntrinsicTimeTrendType.UpTrend,
                BandLevel=balanced?0:1 };
            var trigger=assessment.TriggerEvent with { FuturesItiSignal=signal };
            assessment=assessment with { TriggerEvent=trigger,WorkflowView=assessment.WorkflowView with { TriggerEvent=trigger } };
            var parameters=assessment.WorkflowView.RegimeDiscoveryParameterSet!;
            var cache=host.Services.GetRequiredService<IRegimeDiscoveryMarketSignalCache>();
            var snapshotRequest=RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract(assessment.WorkflowEntityId.ItiSignalEntityId.ContractId),parameters);
            long sequence=0;
            foreach(var requirement in snapshotRequest.Requirements)
            {
                var now=DateTime.UtcNow;
                cache.Upsert(new RegimeDiscoverySignalObservation { Metric=requirement.Metric,
                    SignalKey=new(snapshotRequest.MarketSeriesIdentity,IntrinsicTimeStrategyWorkflowRuntimeIntegrationTests.SignalKind(requirement.Metric),requirement.TimeFrame,requirement.CalculationConfigurationId),
                    Value=FinancialSignal(requirement.Metric,variant),MarketDataAsOfUtc=now,CalculatedAtUtc=now,
                    SourceSequence=++sequence,SchemaVersion=1,CalculationVersion="1",IsWarm=true,IsValid=true,Availability=RegimeDiscoverySignalAvailability.Available,
                    SignalIdentity=$"FinancialPathInput:{assessment.WorkflowId}:{requirement.Metric}:{requirement.TimeFrame}" });
            }
            var identity=RegimeDiscoveryExecutionEntityId.Create(assessment.WorkflowEntityId,assessment.WorkflowId);
            var regime=new ExecuteRegimeDiscoveryPipelineCommand { CommandId=Guid.NewGuid(),EntityId=identity,
                Subject=new(ActorType.Function,ExecuteRegimeDiscoveryPipelineCommand.Actor,ExecuteRegimeDiscoveryPipelineCommand.Verb,identity.Format()),
                WorkflowView=assessment.WorkflowView with { WorkflowRevision=1,CurrentStage=StrategyWorkflowStage.RegimeDiscovery,RegimeDiscovery=new(),MarketCondition=new() },
                InputWorkflowRevision=1,TriggerEvent=assessment.TriggerEvent,CorrelationId=assessment.CorrelationId,CausationId=assessment.TriggerEvent.Id,
                RequestedAtUtc=DateTime.UtcNow,ExpiresAtUtc=DateTime.UtcNow.AddSeconds(20),ParameterSet=parameters,
                ParameterPayloadSha256=RegimeDiscoveryParameterPayload.ComputeSha256(parameters),TargetHorizon=horizon };
            var rd=await producer.RequestFunctionAsync<ExecuteRegimeDiscoveryPipelineCommand,RegimeDiscoveryExecutionEntityId,
                FunctionResult<RegimeDiscoveryPipelineCompletedEvent,RegimeDiscoveryPipelineFailedEvent>>(regime.Subject,regime,regime.EntityId);
            rd.Success.Should().BeTrue(rd.ErrorMessage);rd.Value!.IsCompleted.Should().BeTrue(rd.Value.Failed?.ErrorMessage);
            var regimeEnvelope=rd.Value.Completed!.Result;
            var observed=DateTime.UtcNow;
            assessment=assessment with { RequestedAtUtc=observed,ExpiresAtUtc=observed.AddSeconds(5),RegimeResultEnvelope=regimeEnvelope,
                RegimePayloadSha256=regimeEnvelope.PayloadSha256,WorkflowView=assessment.WorkflowView with {
                    UpdatedAtUtc=observed,RegimeDiscovery=assessment.WorkflowView.RegimeDiscovery with { Result=regimeEnvelope,SourceEventId=rd.Value.Completed.Id,CompletedAtUtc=observed } } };
            var mc=await producer.RequestFunctionAsync<ExecuteMarketConditionAssessmentCommand,MarketConditionAssessmentExecutionId,
                FunctionResult<MarketConditionAssessmentCompletedEvent,MarketConditionAssessmentFailedEvent>>(assessment.Subject,assessment,assessment.EntityId);
            mc.Success.Should().BeTrue(mc.ErrorMessage);mc.Value!.IsCompleted.Should().BeTrue(mc.Value.Failed?.ErrorMessage);
            var selection=await TradeSelectionFixture.Command(variant,horizon,DateTime.UtcNow,scopeId:Random.Shared.Next(100000,900000000),compositionReady:true,compositionIntegrationTiming:true,
                actualAssessmentCommand:assessment,actualAssessmentEnvelope:mc.Value.Completed!.Result);
            var ts=await producer.RequestFunctionAsync<ExecuteTradeSelectionPipelineCommand,TradeSelectionExecutionId,
                FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>>(selection.Subject,selection,selection.EntityId);
            ts.Success.Should().BeTrue(ts.ErrorMessage);ts.Value!.IsCompleted.Should().BeTrue(ts.Value.Failed?.ErrorMessage);
            var selected=ts.Value.Completed!.Result.ReadSelectionResult();selected.Outcome.Should().Be(SelectionOutcome.Selected,selected.SummaryText);
            var composition=await CompositionFixture.Command(variant,horizon,DateTime.UtcNow,integrationTiming:true,
                actualSelectionCommand:selection,actualSelectionResult:selected);
            var oc=await producer.RequestFunctionAsync<ExecuteOrderCompositionPipelineCommand,OrderCompositionExecutionId,
                FunctionResult<OrderCompositionFunctionCompletedEvent,OrderCompositionFunctionFailedEvent>>(composition.Subject,composition,composition.EntityId);
            oc.Success.Should().BeTrue(oc.ErrorMessage);oc.Value!.IsCompleted.Should().BeTrue(oc.Value.Failed?.ErrorMessage);
            var risk=await RiskFixture.Command(horizon:horizon,actualCompositionCommand:composition,actualCompositionResult:oc.Value.Completed!.Result.ReadCompositionResult(),environment:"Emulator");
            var financialBook=await FundFinancialBoundaryAsync(risk);
            var rm=await producer.RequestFunctionAsync<ExecuteRiskManagementPipelineCommand,RiskManagementExecutionId,
                FunctionResult<RiskManagementFunctionCompletedEvent,RiskManagementFunctionFailedEvent>>(risk.Subject,risk,risk.EntityId);
            rm.Success.Should().BeTrue(rm.ErrorMessage);rm.Value!.IsCompleted.Should().BeTrue(rm.Value.Failed?.ErrorMessage);
            rm.Value.Completed!.Result.Outcome.Should().Be(RiskAssessmentOutcome.Approved);
            rm.Value.Completed.Result.TargetHorizon.Should().Be(horizon);rm.Value.Completed.Result.WorkflowId.Should().Be(assessment.WorkflowId);
            risk.RegimeResult.PayloadSha256.Should().Be(regimeEnvelope.PayloadSha256);
            risk.MarketConditionResult.PayloadSha256.Should().Be(mc.Value.Completed!.Result.PayloadSha256);
            risk.SelectionResult.PayloadSha256.Should().Be(ts.Value.Completed.Result.PayloadSha256);
            risk.CompositionResult.PayloadSha256.Should().Be(oc.Value.Completed.Result.PayloadSha256);
            await VerifyCalculatedRiskFinancialBoundaryAsync(risk,rm.Value.Completed,composition.WorkflowView,financialBook);
        }
        finally { await supervisor.ShutdownAsync();await producer.StopAsync(); }
    }
    public static IEnumerable<object[]> FiveStageCases()=>from horizon in new[] {TimeFrameType.Daily,TimeFrameType.Weekly,TimeFrameType.Monthly}
        from variant in CompositionFixture.Variants select new object[] {horizon,variant};

    static decimal FinancialSignal(RegimeDiscoverySignalMetric metric,string variant)
    {
        var balanced=variant.Contains("Balanced",StringComparison.Ordinal);
        var bearish=variant.Contains("Bear",StringComparison.Ordinal)||variant=="ShortFuture";
        var value=IntrinsicTimeStrategyWorkflowRuntimeIntegrationTests.SignalValue(metric);
        if(metric==RegimeDiscoverySignalMetric.PriorVolatilityComposite && variant.StartsWith("Long",StringComparison.Ordinal)&&variant.Contains("IronCondor",StringComparison.Ordinal)) return 0;
        // Keep price inside its rolling range so both debit and credit directional variants see a trend.
        if(metric==RegimeDiscoverySignalMetric.RollingHigh20) return 110;
        if(metric==RegimeDiscoverySignalMetric.RollingLow20) return 90;
        if(metric==RegimeDiscoverySignalMetric.BreakoutDistanceAtr) return 0;
        if(balanced) return metric switch {
            RegimeDiscoverySignalMetric.CurrentPrice or RegimeDiscoverySignalMetric.Ema20 or RegimeDiscoverySignalMetric.Ema50 or RegimeDiscoverySignalMetric.Ema200=>100,
            RegimeDiscoverySignalMetric.Rsi14=>50,
            RegimeDiscoverySignalMetric.PlusDi14 or RegimeDiscoverySignalMetric.MinusDi14=>20,
            RegimeDiscoverySignalMetric.Ema20Slope or RegimeDiscoverySignalMetric.Ema50Slope or RegimeDiscoverySignalMetric.Ema200Slope or
            RegimeDiscoverySignalMetric.Rsi14Slope or RegimeDiscoverySignalMetric.MacdHistogram or RegimeDiscoverySignalMetric.BollingerPosition or
            RegimeDiscoverySignalMetric.Ema20Interaction or RegimeDiscoverySignalMetric.ItiBandLevel=>0,
            _=>value };
        if(!bearish) return value;
        return metric switch {
            RegimeDiscoverySignalMetric.CurrentPrice or RegimeDiscoverySignalMetric.Ema20 or RegimeDiscoverySignalMetric.Ema50 or RegimeDiscoverySignalMetric.Ema200=>200-value,
            RegimeDiscoverySignalMetric.Rsi14=>100-value,
            RegimeDiscoverySignalMetric.PlusDi14=>15,
            RegimeDiscoverySignalMetric.MinusDi14=>30,
            RegimeDiscoverySignalMetric.Ema20Slope or RegimeDiscoverySignalMetric.Ema50Slope or RegimeDiscoverySignalMetric.Ema200Slope or
            RegimeDiscoverySignalMetric.Rsi14Slope or RegimeDiscoverySignalMetric.MacdHistogram or RegimeDiscoverySignalMetric.BollingerPosition or
            RegimeDiscoverySignalMetric.Ema20Interaction or RegimeDiscoverySignalMetric.ItiDirection or RegimeDiscoverySignalMetric.Tdi=>-value,
            _=>value };
    }
    sealed class FinancialPathMarketInputs(Func<ExecuteMarketConditionAssessmentCommand> input):IMarketConditionAssessmentSnapshotProvider
    {
        public ValueTask<MarketConditionAssessmentSnapshot> CaptureAsync(MarketConditionAssessmentParameterSet parameters,DateTime at,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var snapshot=TradeSelectionFixture.Snapshot(input());
            // These labelled source observations are captured at this calculation's cut.
            // Preserve the real upstream result and normal confidence/freshness policy.
            return ValueTask.FromResult((snapshot with { Observations=snapshot.Observations.Select(x=>x with {
                ObservedAtUtc=snapshot.EvaluatedAtUtc,ReceivedAtUtc=snapshot.EvaluatedAtUtc }).ToArray() }).Seal());
        }
    }
}
