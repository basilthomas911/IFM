using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
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

    /// <summary>Identifies the command owner of a strategy's exit workflow.</summary>
    public static string Owner(TradeStrategyKind kind) => kind switch
    {
        TradeStrategyKind.FuturesOutright => StartFuturesExitPositionWorkflowCommand.Actor,
        TradeStrategyKind.VerticalSpread => StartVerticalSpreadExitPositionWorkflowCommand.Actor,
        TradeStrategyKind.IronCondor => StartIronCondorExitPositionWorkflowCommand.Actor,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    /// <summary>Identifies the durable continuation for an accepted workflow transition.</summary>
    public static DurableProjectionRequirement DurableProjection(TradeStrategyKind kind)
    {
        var actor = Owner(kind);
        return new(actor + "CommandActor", actor + "EventProjector", EventProjectorStageType.ApplyProjection);
    }
}
