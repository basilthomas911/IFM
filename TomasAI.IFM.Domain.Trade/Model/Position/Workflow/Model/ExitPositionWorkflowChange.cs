using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
/// <summary>Immutable exit workflow proposal retaining exact plan identity.</summary>
internal sealed record ExitPositionWorkflowChange(StrategyTradePlanSnapshot ExitPlan, TradeStrategyKind StrategyKind, Guid SourcePlanEventId, bool AlreadyStarted = false, string? RejectionReason = null);
