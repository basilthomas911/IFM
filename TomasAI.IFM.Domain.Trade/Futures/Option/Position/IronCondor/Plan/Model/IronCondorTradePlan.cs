using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
public sealed record IronCondorTradePlan(StrategyTradePlanSnapshot Snapshot);
