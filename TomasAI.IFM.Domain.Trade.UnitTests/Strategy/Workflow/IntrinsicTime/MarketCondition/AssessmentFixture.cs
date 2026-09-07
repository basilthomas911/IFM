using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

internal static class AssessmentFixture
{
    public static ExecuteMarketConditionAssessmentCommand Command(TimeFrameType horizon = TimeFrameType.Daily, DateTime? atUtc = null, string contractId = "ESZ6")
    {
        var at = atUtc ?? new DateTime(2026, 8, 28, 14, 0, 0, DateTimeKind.Utc);
        var workflowId = new StrategyWorkflowId(Guid.NewGuid());
        var itiId = FuturesItiSignalEntityId.Create(contractId, DateOnly.FromDateTime(at), horizon);
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(itiId);
        var trigger = new FuturesItiSignalGeneratedEvent
        {
            Id = Guid.Parse("019917f7-1c00-7000-8000-000000000002"), EntityId = itiId, CreatedOn = at.AddSeconds(-1),
            FuturesItiSignal = new FuturesItiSignalV2ReadModel { ContractId = contractId, ValueDate = itiId.ValueDate,
                TimeFrameStartValueDate = itiId.ValueDate, TimePeriod = horizon, SequenceId = 1, IntrinsicTime = at,
                IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
                IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged, BandLevel = 1d, ReversalLevel = 0.1d,
                TradingDays = 1 }
        };
        var upstream = new RegimeDiscoveryResult
        {
            ResultId = Guid.Parse("019917f7-1c00-7000-8000-000000000003"), WorkflowId = workflowId,
            EntityId = entity, TriggerEventId = trigger.Id, TargetHorizon = horizon, ProducedAtUtc = at.AddSeconds(-2),
            OverallConfidence = 0.90m, OverallQuality = RegimeOverallQuality.High,
            Trend = new() { IsComplete = true, Direction = RegimeDirection.Up },
            Volatility = new() { IsComplete = true, Change = VolatilityRegimeChange.Stable },
            MarketStructure = new() { IsComplete = true, Classification = MarketStructureClassification.Trending,
                Direction = RegimeDirection.Up },
            Decision = new() { IsComplete = true, Direction = RegimeDirection.Up, Confidence = 0.90m,
                Quality = RegimeOverallQuality.High }
        };

        var rp = RegimeDiscoveryParameterSet.CreateDefault(Guid.NewGuid(), Guid.NewGuid(), horizon);
        var p = MarketConditionAssessmentParameterSet.CreateDefault("ES.Standard", horizon, Guid.NewGuid(), rp.ParameterSetId, rp.Version);
        var regime = upstream with
        {
            RegimeDiscoveryParameterSetId = rp.ParameterSetId, RegimeDiscoveryParameterSetVersion = rp.Version,
            MarketDataAsOfUtc = at.AddSeconds(-1),
            Decision = upstream.Decision with { StructureClassification = MarketStructureClassification.Trending, VolatilityChange = VolatilityRegimeChange.Stable }
        };
        var envelope = StrategyStageResultEnvelope.Create(regime.ResultId, nameof(RegimeDiscoveryResult), RegimeDiscoveryResult.CurrentSchemaVersion,
            MessagePackSerializer.Serialize(regime), regime.MarketDataAsOfUtc, regime.ProducedAtUtc);
        var binding = new MarketConditionAssessmentBinding { Parameters = p, PayloadSha256 = MarketConditionAssessmentHash.Parameters(p) };
        var v = new IntrinsicTimeStrategyWorkflowView
        {
            EntityId = entity, WorkflowId = workflowId, CorrelationId=trigger.Id, StartedAtUtc=at.AddSeconds(-2), Status = WorkflowStrategyMachineStatus.Started,
            CurrentStage = StrategyWorkflowStage.MarketCondition, WorkflowRevision = 2, FundId = 1,
            UpdatedAtUtc = at, TriggerEventId = trigger.Id,
            TriggerEvent = trigger, RegimeDiscoveryParameterSet = rp,
            RegimeDiscoveryParameterPayloadSha256 = RegimeDiscoveryParameterPayload.ComputeSha256(rp),
            ExpiresAtUtc = at.AddMinutes(1), AssessmentBinding = binding,
            MarketCondition = new() {ProcessingStatus=StrategyActorProcessingStatus.Processing,InputWorkflowRevision=2,StartedAtUtc=at},
            RegimeDiscovery = new() { ProcessingStatus = StrategyActorProcessingStatus.Completed, CompletedAtUtc = at.AddSeconds(-1), Result = envelope }
        };
        var id = new MarketConditionAssessmentExecutionId(v.EntityId, v.WorkflowId);
        return new()
        {
            CommandId = Guid.NewGuid(), EntityId = id,CorrelationId=v.CorrelationId,CausationId=trigger.Id,
            Subject = new(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, ExecuteMarketConditionAssessmentCommand.Verb, id.Format()),
            WorkflowView = v, TriggerEvent = trigger, ParameterSet = p, ParameterPayloadSha256 = binding.PayloadSha256,
            MarketProfileId = p.MarketProfileId, InstrumentRoot = p.InstrumentRoot, TargetHorizon = horizon,
            InputWorkflowRevision = v.WorkflowRevision, RequestedAtUtc = at, ExpiresAtUtc = at.AddSeconds(5),
            RegimeResultEnvelope = envelope, RegimePayloadSha256 = envelope.PayloadSha256
        };
    }
}
