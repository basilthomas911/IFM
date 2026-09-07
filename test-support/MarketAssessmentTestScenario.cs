using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

namespace TomasAI.IFM.Tests.Support;

public sealed class MarketAssessmentTestScenario
{
    public MarketConditionAssessmentResult Result { get; }
    public IntrinsicTimeStrategyWorkflowView View { get; }
    public MarketConditionAssessmentCompletedEvent Projection { get; }
    public StartTradeSelectionPipelineCommand Selection { get; }
    public FundMandateReadModel Mandate { get; }
    public MarketAssessmentTestScenario(TimeFrameType horizon=TimeFrameType.Daily,string scenario="directional")
    {
        var sample=MarketConditionAssessmentReferenceGenerator.CreateScenario(horizon,scenario);
        var c=sample.Command; var at=c.RequestedAtUtc;
        Result=new MarketConditionAssessmentCalculator().Calculate(c,sample.Snapshot,c.CommandId);
        var envelope=StrategyStageResultEnvelope.Create(Result.ResultId,nameof(MarketConditionAssessmentResult),1,MessagePackSerializer.Serialize(Result),at,at);
        View=c.WorkflowView with { FundId=5001,WorkflowRevision=3,CurrentStage=StrategyWorkflowStage.TradeSelection,
            MarketCondition=new(){InputWorkflowRevision=2,ProcessingStatus=StrategyActorProcessingStatus.Completed,SourceEventId=Result.ResultId,Result=envelope,CompletedAtUtc=at} };
        Projection=new(){Id=Result.ResultId,CommandId=c.CommandId,EntityId=c.WorkflowEntityId,WorkflowId=c.WorkflowId,InputWorkflowRevision=2,
            Result=envelope,Snapshot=sample.Snapshot,RequestFingerprint=c.Fingerprint(),ParameterPayloadSha256=c.ParameterPayloadSha256};
        var state=new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(new WorkflowStrategyStateUpdatedEvent { State=View,WorkflowId=View.WorkflowId,WorkflowRevision=3,EntityId=View.EntityId },addEvent:false);
        Selection=new(){CommandId=Guid.NewGuid(),EntityId=View.EntityId,WorkflowId=View.WorkflowId,InputWorkflowRevision=3,
            WorkflowState=state.LatestWorkflow!,TriggerEvent=c.TriggerEvent,RequestedAtUtc=at,ExpectedCompletionAtUtc=at.AddMinutes(1)};
        Mandate=new(){PortfolioId=1001,FundId=5001,FundCode="Qualification",Name="Qualification",FundMandateVersion=7,SchemaVersion=2,TradingYear=2026,
            OperatingState=FundOperatingState.Active,EffectiveFromUtc=at.AddDays(-1),DecisionHorizon=horizon.ToString(),Objective="Qualification",
            UnderlyingUniverse=["ES"],EligibleAssetTypes=["Futures"],PermittedDirections=["Bullish","Bearish","Neutral"],
            PermittedConditions=Enum.GetNames<AssessmentCondition>(),PermittedTradeFamilies=["Futures"],PermittedTradeStrategyFamilies=[new(1,2)],CreatedOnUtc=at.AddDays(-1),CreatedBy="test"};
    }
}
