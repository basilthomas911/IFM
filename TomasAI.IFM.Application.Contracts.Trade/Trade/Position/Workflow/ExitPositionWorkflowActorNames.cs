using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

/// <summary>Stable actor names for each strategy-specific exit pipeline.</summary>
public static class ExitPositionWorkflowActorNames
{
    public const string IronCondorOrderComposer = "IronCondorExitOrderCompositionFunction";
    public const string IronCondorRiskManager = "IronCondorPositionExitRiskFunction";
    public const string VerticalSpreadOrderComposer = "VerticalSpreadExitOrderCompositionFunction";
    public const string VerticalSpreadRiskManager = "VerticalSpreadPositionExitRiskFunction";
    public const string FuturesOrderComposer = "FuturesExitOrderCompositionFunction";
    public const string FuturesRiskManager = "FuturesPositionExitRiskFunction";
}
