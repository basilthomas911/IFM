using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
[MessagePackObject]
public sealed record RiskObservation([property: Key(0)] WorkflowStrategyStateUpdatedEvent Snapshot,
    [property: Key(1)] RiskAssessmentResult? Calculation, [property: Key(2)] string CurrentAuthority,
    [property: Key(3)] string FundSynchronization, [property: Key(4)] DateTime CheckedAtUtc,
    [property: Key(5)] FundOrderProjectionReadModel? FundOrder, [property: Key(6)] bool ProjectionBehind, [property: Key(7)] bool CalculationAccepted = false);
