using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;

/// <summary>Derives the immutable identities reused by workflow retries and receipt validation.</summary>
internal static class ExitWorkflowLifecycleIdentity
{
    internal static Guid Function(ExitPositionWorkflowId workflow, string verb) =>
        TradePlanContractIdentity.DeterministicId($"{workflow.Format()}|{verb}");

    internal static Guid Transition(ExitPositionWorkflowId workflow, string verb, Guid predecessor) =>
        TradeHandoffIdentity.Create("exit-workflow-transition", workflow.Format(), verb, predecessor.ToString("N"));

    internal static string CompositionFingerprint(ExitPositionWorkflowStartedEvent started) =>
        FinancialCanonicalHash.Compute(new
        {
            started.EntityId, started.StrategyKind, started.SourcePlanEventId, Plan = started.ExitPlan.ContentHash
        });

    internal static string RiskFingerprint(ExitOrderComposition composition, Guid compositionReceiptId) =>
        FinancialCanonicalHash.Compute(new
        {
            EntityId = composition.WorkflowId, composition.StrategyKind,
            CompositionEventId = compositionReceiptId, composition.CompositionHash
        });

    internal static (string Composer, string Risk) Functions(TradeStrategyKind strategy) => strategy switch
    {
        TradeStrategyKind.FuturesOutright => (ExitPositionWorkflowActorNames.FuturesOrderComposer, ExitPositionWorkflowActorNames.FuturesRiskManager),
        TradeStrategyKind.VerticalSpread => (ExitPositionWorkflowActorNames.VerticalSpreadOrderComposer, ExitPositionWorkflowActorNames.VerticalSpreadRiskManager),
        TradeStrategyKind.IronCondor => (ExitPositionWorkflowActorNames.IronCondorOrderComposer, ExitPositionWorkflowActorNames.IronCondorRiskManager),
        _ => throw new InvalidDataException($"Unsupported exit workflow strategy {strategy}.")
    };

    internal static Guid OrderHandoff(PortfolioCloseRiskDecision decision, string action)
    {
        var order = decision.TradeOrder ?? throw new InvalidDataException("Accepted exit risk lacks its Trade Order.");
        var seed = TradeHandoffIdentity.Create("exit-order", decision.PortfolioCompletedEventId.ToString("N"), order.Id.Format());
        return TradeHandoffIdentity.Create("exit-order-" + action, seed.ToString("N"), order.Id.Format());
    }
}
