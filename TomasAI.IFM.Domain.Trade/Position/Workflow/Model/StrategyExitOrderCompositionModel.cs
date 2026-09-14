using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Model;

/// <summary>Creates the exact broker-neutral reduction requested by a strategy exit decision.</summary>
public static class StrategyExitOrderCompositionModel
{
    public static ExitOrderComposition Compose(ExitPositionWorkflowStartedEvent started, DateTime composedAtUtc)
    {
        var position = started.ExitPlan.Position;
        if (!position.Id.IsValid || !position.IsOpen || position.StrategyKind != started.StrategyKind ||
            position.Legs.Length == 0 || started.ExitPlan.State != TradePlanState.ExitRequired ||
            !started.ExitPlan.RequiresExit)
            throw new InvalidOperationException("EXIT.COMPOSITION.INVALID_PLAN");

        var component = new TradeOrderComponentDefinition
        {
            ComponentId = TradePlanContractIdentity.DeterministicId(
                $"{started.EntityId.Format()}|close-component"),
            StrategyKind = started.StrategyKind,
            ReservedTradeId = position.Id.Trade.TradeId,
            PermitBalancedPartialAcceptance = false,
            Legs = position.Legs.Select(leg => new TradeLegDefinition
            {
                TradeLegId = leg.TradeLegId,
                ContractId = leg.ContractId,
                ContractKey = leg.ContractKey,
                AssetFamily = leg.AssetFamily,
                SignedQuantity = checked(-leg.SignedQuantity),
                LimitPrice = started.ExitPlan.Action == TradePlanAction.ExitAtLimit ? leg.CurrentPrice : null,
                Expiry = leg.Expiry,
                Strike = leg.Strike,
                PutCall = leg.PutCall
            }).ToArray()
        };
        if (component.Legs.Any(leg => leg.SignedQuantity == 0 || string.IsNullOrWhiteSpace(leg.ContractId)))
            throw new InvalidOperationException("EXIT.COMPOSITION.INVALID_REMAINING_LEG");

        var hash = TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialCanonicalHash.Compute(new
        {
            Workflow = started.EntityId.Format(),
            Position = position.Id.Format(),
            started.StrategyKind,
            started.ExitPlan.Action,
            Legs = component.Legs.Select(leg => new
            {
                leg.TradeLegId, leg.ContractId, leg.SignedQuantity, leg.LimitPrice
            }).ToArray()
        });
        return new ExitOrderComposition
        {
            WorkflowId = started.EntityId,
            StrategyKind = started.StrategyKind,
            Position = position,
            Component = component,
            ExitAction = started.ExitPlan.Action,
            CompositionHash = hash,
            ComposedAtUtc = DateTime.SpecifyKind(composedAtUtc, DateTimeKind.Utc),
            PositionType = TradeOrderPositionType.Closing
        };
    }
}
