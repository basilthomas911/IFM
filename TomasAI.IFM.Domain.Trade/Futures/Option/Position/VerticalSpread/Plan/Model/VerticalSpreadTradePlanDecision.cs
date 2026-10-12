using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Model;
public readonly record struct VerticalSpreadTradePlanDecision(TradePlanState State, TradePlanAction Action, bool RequiresExit, string ReasonCode, string Explanation);
