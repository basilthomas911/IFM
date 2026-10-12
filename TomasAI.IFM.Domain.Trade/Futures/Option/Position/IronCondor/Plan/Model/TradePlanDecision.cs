using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
public readonly record struct TradePlanDecision(TradePlanState State, TradePlanAction Action, bool RequiresExit, string ReasonCode, string Explanation);
