using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
public readonly record struct FuturesTradePlanDecision(TradePlanState State, TradePlanAction Action, bool RequiresExit, string ReasonCode, string Explanation);
