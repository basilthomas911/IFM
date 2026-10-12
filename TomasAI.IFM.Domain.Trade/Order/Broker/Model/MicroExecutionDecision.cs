namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;
/// <summary>Material deterministic decision returned by the policy.</summary>
public sealed record MicroExecutionDecision(MicroExecutionAction Action, MicroExecutionActionMask AllowedActions, decimal? NewLimit, string Reason);
