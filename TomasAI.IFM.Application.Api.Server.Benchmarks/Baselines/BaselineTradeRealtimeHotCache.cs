// Frozen from commit 51075e3eb before this optimization pass. Benchmark reference only.
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;

/// <summary>Bounded per-monitor whole-trade read model. Serialized writes; atomic, non-consuming reads.</summary>
public sealed class BaselineTradeRealtimeHotCache
{
    readonly object gate = new();
    readonly HashSet<Guid> retiredGenerations = [];
    readonly HashSet<Guid> retiredMonitorGenerations = [];
    readonly Queue<Guid> retiredSourceOrder = new(), retiredMonitorOrder = new();
    readonly TimeProvider clock;
    TradeRealtimeSnapshot current;
    TradeRealtimeSnapshot? remote;
    bool retired;

    public BaselineTradeRealtimeHotCache(EstablishedTradeDefinition trade, DateOnly valueDate, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(trade);
        clock = time ?? TimeProvider.System;
        current = new()
        {
            TradeId = trade.Id, PositionId = StrategyPositionId.Create(trade.Id, trade.StrategyKind),
            ValueDate = valueDate, MonitoringGeneration = Guid.NewGuid(),
            CompositionVersion = trade.EvidenceRevision,
            PublishedAtUtc = clock.GetUtcNow(),
            Legs = trade.Legs.Select(leg => new TradeLegRealtimeSnapshot { Definition = leg }).ToImmutableArray()
        };
    }

    public TradeRealtimeSnapshot Read() => Volatile.Read(ref current);
    /// <summary>Fences work captured by a router before this cache was removed or replaced.</summary>
    public void Retire() { lock (gate) retired = true; }

    /// <summary>Replaces a client mirror atomically after subscribe-before-read catch-up.</summary>
    public bool AcceptSnapshot(TradeRealtimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (gate)
        {
            if (retired || snapshot.SchemaVersion != 1 || snapshot.TradeId != current.TradeId || snapshot.ValueDate != current.ValueDate
                || snapshot.PositionId != current.PositionId || snapshot.MonitoringGeneration == Guid.Empty || snapshot.SnapshotRevision < 0
                || snapshot.CompositionVersion < current.CompositionVersion
                || retiredMonitorGenerations.Contains(snapshot.MonitoringGeneration)
                || snapshot.Legs.Length is < 1 or > 64
                || snapshot.Legs.Select(leg => leg.Definition.TradeLegId).Distinct().Count() != snapshot.Legs.Length
                || snapshot.CompositionVersion == current.CompositionVersion && (snapshot.Legs.Length != current.Legs.Length
                    || snapshot.Legs.Any(leg => !current.Legs.Any(expected => expected.Definition.TradeLegId == leg.Definition.TradeLegId
                        && expected.Definition.ContractId == leg.Definition.ContractId)))) return false;
            if (remote is { } previous && (snapshot.MonitoringGeneration == previous.MonitoringGeneration
                ? snapshot.SnapshotRevision <= previous.SnapshotRevision
                : snapshot.PublishedAtUtc <= previous.PublishedAtUtc)) return false;
            if (remote is { } prior && prior.MonitoringGeneration != snapshot.MonitoringGeneration)
            {
                RememberRetired(retiredMonitorGenerations, retiredMonitorOrder, prior.MonitoringGeneration);
            }
            remote = snapshot;
            Volatile.Write(ref current, Qualify(snapshot));
            return true;
        }
    }

    public bool Apply(FuturesOptionTickDataV2ReadModel observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        lock (gate)
        {
            if (retired || observation.ValueDate != current.ValueDate) return false;
            var index = Enumerable.Range(0, current.Legs.Length).FirstOrDefault(i => current.Legs[i].Definition.ContractId == observation.ContractId, -1);
            if (index < 0) return false;
            var leg = current.Legs[index];
            var previous = leg.Observation;
            if (observation.IsTradeObservation)
            {
                if (observation.ObservedAtUtc < leg.LastTradeAtUtc
                    || observation.ObservedAtUtc == leg.LastTradeAtUtc && observation.TickId <= leg.LastTradeSequence) return false;
                Publish(current with { Legs = current.Legs.SetItem(index, leg with
                {
                    LastTradePrice = observation.OptionPrice, LastTradeSequence = observation.TickId,
                    LastTradeAtUtc = observation.ObservedAtUtc
                }) });
                return true;
            }
            if (retiredGenerations.Contains(observation.SourceGeneration)) return false;
            if (previous is not null)
            {
                if (previous.SourceGeneration != observation.SourceGeneration)
                {
                    if (observation.ObservedAtUtc <= previous.ObservedAtUtc || observation.IsPricingUpdate) return false;
                    RememberRetired(retiredGenerations, retiredSourceOrder, previous.SourceGeneration);
                }
                else if (observation.TickId < previous.TickId
                    || observation.ObservationRevision > 0 && observation.ObservationRevision <= previous.ObservationRevision
                    || observation.TickId == previous.TickId && (!observation.IsPricingUpdate || observation.ObservationRevision == 0)) return false;
            }
            Publish(current with { Legs = current.Legs.SetItem(index, leg with { Observation = observation }) });
            return true;
        }
    }

    static void RememberRetired(HashSet<Guid> generations, Queue<Guid> order, Guid generation)
    {
        if (!generations.Add(generation)) return;
        order.Enqueue(generation);
        if (order.Count > 128) generations.Remove(order.Dequeue());
    }

    public bool Apply(StrategyPositionSnapshot position)
    {
        ArgumentNullException.ThrowIfNull(position);
        lock (gate)
        {
            if (retired || !Matches(position) || current.Position is { } old &&
                (position.RouteGeneration < old.RouteGeneration || position.RouteGeneration == old.RouteGeneration
                    && position.PositionSequence <= old.PositionSequence)) return false;
            Publish(WithPosition(current, position));
            return true;
        }
    }

    public bool Apply(StrategyTradePlanSnapshot plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (gate)
        {
            if (retired || plan.ValueDate != current.ValueDate || !Matches(plan.Position)
                || current.Position is { } latest && (plan.Position.RouteGeneration < latest.RouteGeneration
                    || plan.Position.RouteGeneration == latest.RouteGeneration && plan.Position.PositionSequence < latest.PositionSequence)
                || current.Plan is { } old && (plan.Position.RouteGeneration < old.Position.RouteGeneration
                    || plan.Position.RouteGeneration == old.Position.RouteGeneration && plan.PlanRevision <= old.PlanRevision)) return false;
            var next = current;
            if (next.Position is null || plan.Position.RouteGeneration > next.Position.RouteGeneration
                || plan.Position.RouteGeneration == next.Position.RouteGeneration && plan.Position.PositionSequence > next.Position.PositionSequence)
                next = WithPosition(next, plan.Position);
            Publish(next with { Plan = plan });
            return true;
        }
    }

    /// <summary>Expires quote/risk state without requiring another tick. Returns whether a revision was published.</summary>
    public bool Expire()
    {
        lock (gate)
        {
            if (retired) return false;
            var next = Qualify(current);
            if (next.Legs.SequenceEqual(current.Legs) && next.PlanStatus == current.PlanStatus) return false;
            Publish(next);
            return true;
        }
    }

    bool Matches(StrategyPositionSnapshot position) => position.Id == current.PositionId
        && position.ValueDate == current.ValueDate && position.Legs.Length == current.Legs.Length
        && position.Legs.All(mark => current.Legs.Any(leg => leg.Definition.TradeLegId == mark.TradeLegId
            && leg.Definition.ContractId == mark.ContractId));

    static TradeRealtimeSnapshot WithPosition(TradeRealtimeSnapshot snapshot, StrategyPositionSnapshot position) => snapshot with
    {
        Position = position,
        Legs = snapshot.Legs.Select(leg => leg with { Position = position.Legs.Single(mark => mark.TradeLegId == leg.Definition.TradeLegId) }).ToImmutableArray()
    };

    public TradeRealtimeSnapshot Qualify(TradeRealtimeSnapshot snapshot)
    {
        var now = clock.GetUtcNow();
        var plan = snapshot.Plan;
        var detail = plan?.IronCondorTradePlanSnapshot;
        var planCurrent = plan is not null && snapshot.Position is { } position
            && plan.Position.RouteGeneration == position.RouteGeneration && plan.Position.PositionSequence == position.PositionSequence
            && detail?.IsComplete == true && detail.RiskValidUntilUtc is { } deadline && deadline > now.UtcDateTime
            && snapshot.Legs.All(leg => leg.Observation is { } quote && quote.GreeksAvailable == true
                && quote.ValidUntilUtc > now && detail.Legs.Any(input => input.TradeLegId == leg.Definition.TradeLegId
                    && input.QuoteAtUtc >= quote.ObservedAtUtc.UtcDateTime));
        var legs = snapshot.Legs.Select(leg =>
        {
            if (leg.Observation is not { } quote) return leg;
            var stale = quote.ValidUntilUtc != default && quote.ValidUntilUtc <= now;
            return leg with
            {
                QuoteStatus = stale ? "Stale" : "Available",
                PricingStatus = stale ? "Stale" : quote.GreeksAvailable == true ? "Available"
                    : quote.PricingFailure == "CalculationPending" ? "Pending" : "Unavailable",
                Scenario = planCurrent ? detail!.Legs.FirstOrDefault(input => input.TradeLegId == leg.Definition.TradeLegId) : null
            };
        }).ToImmutableArray();
        var complete = legs.Length > 0 && legs.All(leg => leg.PricingStatus == "Available")
            && legs.Select(leg => leg.Observation!.SourceGeneration).Distinct().Count() == 1
            && legs.Max(leg => leg.Observation!.ObservedAtUtc) - legs.Min(leg => leg.Observation!.ObservedAtUtc) <= TimeSpan.FromSeconds(1);
        double? Risk(Func<FuturesOptionTickDataV2ReadModel, double> read) => complete
            ? legs.Sum(leg => read(leg.Observation!) * (leg.Position?.SignedQuantity ?? leg.Definition.SignedQuantity)) : null;
        var underlying = legs.Where(leg => leg.QuoteStatus == "Available" && leg.Observation?.UnderlyingPrice > 0)
            .OrderByDescending(leg => leg.Observation!.CalculatedAtUtc).FirstOrDefault()?.Observation?.UnderlyingPrice;
        return snapshot with { Legs = legs, PlanStatus = planCurrent ? "Available" : plan is null ? "Unavailable" : "Stale",
            Summary = snapshot.Summary with { UnderlyingPrice = underlying, NetDelta = Risk(quote => quote.Delta),
                NetGamma = Risk(quote => quote.Gamma), NetVega = Risk(quote => quote.Vega), NetTheta = Risk(quote => quote.Theta),
                NetRho = Risk(quote => quote.Rho), PositionValue = snapshot.Position?.MarketValue, DailyPnlPoints = snapshot.Position?.DailyPnl },
            Spreads = planCurrent ? snapshot.Spreads : snapshot.Spreads.Select(spread => spread with { OtmProbability = null, ForwardPrice = null }).ToImmutableArray(),
            Status = complete ? "Available"
            : legs.Any(leg => leg.QuoteStatus == "Stale") ? "Stale" : "Pending" };
    }

    void Publish(TradeRealtimeSnapshot next)
    {
        next = Qualify(next);
        var plan = next.PlanStatus == "Available" ? next.Plan?.IronCondorTradePlanSnapshot : null;
        var spreads = new List<TradeSpreadRealtimeSnapshot>();
        foreach (var right in new byte[] { 2, 1 })
        {
            var legs = next.Legs.Where(leg => leg.Definition.PutCall == right).ToArray();
            var complete = legs.Length == 2 && legs.All(leg => leg.Position is not null && leg.Definition.CashMultiplier > 0);
            var quantity = complete && legs.Select(leg => Math.Abs(leg.Position!.SignedQuantity)).Distinct().Count() == 1
                && legs.Sum(leg => leg.Position!.SignedQuantity) == 0 ? Math.Abs(legs[0].Position!.SignedQuantity) : 0;
            spreads.Add(new()
            {
                PutCall = right,
                NetSpread = complete && quantity > 0 ? legs.Sum(leg => leg.Position!.CurrentPrice * leg.Position.SignedQuantity) / quantity : null,
                TradeValue = complete ? legs.Sum(leg => leg.Position!.CurrentPrice * leg.Position.SignedQuantity * leg.Definition.CashMultiplier) : null,
                TradePnl = complete ? legs.Sum(leg => (leg.Position!.CurrentPrice - leg.Position.OpeningPrice) * leg.Position.SignedQuantity * leg.Definition.CashMultiplier) : null,
                OtmProbability = plan?.IsComplete == true ? right == 2 ? plan.PutOTMProbability : plan.CallOTMProbability : null,
                ForwardPrice = plan?.IsComplete == true ? right == 2 ? plan.PutForwardPrice : plan.CallForwardPrice : null
            });
        }
        Volatile.Write(ref current, next with { SnapshotRevision = remote is null ? current.SnapshotRevision + 1 : current.SnapshotRevision,
            QualityRevision = remote is null ? 0 : current.QualityRevision + 1,
            Summary = next.Summary with { NetSpread = spreads.Count == 2 && spreads.All(spread => spread.NetSpread.HasValue)
                ? spreads.Sum(spread => spread.NetSpread!.Value) : null },
            PublishedAtUtc = clock.GetUtcNow(), Spreads = spreads.ToImmutableArray() });
    }
    public static System.Collections.Immutable.ImmutableArray<TradeSpreadRealtimeSnapshot> CalculateSpreads(TradeRealtimeSnapshot next)
    {
        var plan = next.PlanStatus == "Available" ? next.Plan?.IronCondorTradePlanSnapshot : null;
        var spreads = new List<TradeSpreadRealtimeSnapshot>();
        foreach (var right in new byte[] { 2, 1 })
        {
            var legs = next.Legs.Where(leg => leg.Definition.PutCall == right).ToArray();
            var complete = legs.Length == 2 && legs.All(leg => leg.Position is not null && leg.Definition.CashMultiplier > 0);
            var quantity = complete && legs.Select(leg => Math.Abs(leg.Position!.SignedQuantity)).Distinct().Count() == 1
                && legs.Sum(leg => leg.Position!.SignedQuantity) == 0 ? Math.Abs(legs[0].Position!.SignedQuantity) : 0;
            spreads.Add(new()
            {
                PutCall = right,
                NetSpread = complete && quantity > 0 ? legs.Sum(leg => leg.Position!.CurrentPrice * leg.Position.SignedQuantity) / quantity : null,
                TradeValue = complete ? legs.Sum(leg => leg.Position!.CurrentPrice * leg.Position.SignedQuantity * leg.Definition.CashMultiplier) : null,
                TradePnl = complete ? legs.Sum(leg => (leg.Position!.CurrentPrice - leg.Position.OpeningPrice) * leg.Position.SignedQuantity * leg.Definition.CashMultiplier) : null,
                OtmProbability = plan?.IsComplete == true ? right == 2 ? plan.PutOTMProbability : plan.CallOTMProbability : null,
                ForwardPrice = plan?.IsComplete == true ? right == 2 ? plan.PutForwardPrice : plan.CallForwardPrice : null
            });
        }
        return spreads.ToImmutableArray();
    }
}
