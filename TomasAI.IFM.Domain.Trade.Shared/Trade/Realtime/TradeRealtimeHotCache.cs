using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

/// <summary>Bounded per-monitor whole-trade read model. Serialized writes; atomic, non-consuming reads.</summary>
public sealed class TradeRealtimeHotCache
{
    readonly object gate = new();
    readonly HashSet<Guid> retiredMonitorGenerations = [];
    readonly Queue<Guid> retiredMonitorOrder = new();
    readonly TimeProvider clock;
    TradeRealtimeSnapshot current;
    TradeRealtimeSnapshot? remote;
    StrategyTradePlanSnapshot? publishedPlan;
    StrategyTradePlanSnapshot? publishedAvailablePlan;
    bool retired;
    DateTimeOffset nextQualityCheck = DateTimeOffset.MaxValue;

    public TradeRealtimeHotCache(EstablishedTradeDefinition trade, DateOnly valueDate, TimeProvider? time = null)
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

    /// <summary>Captures live quotes and positions while exposing a new Iron Condor plan only when Daily PnL changes.</summary>
    /// <returns>A notification payload; the full latest calculated plan remains available through <see cref="Read"/>.</returns>
    public TradeRealtimeSnapshot ReadForPublication()
    {
        lock (gate)
        {
            if (current.Plan is not { } plan || plan.Position.StrategyKind != TradeStrategyKind.IronCondor)
                return current;
            if (TradePlanPublication.ShouldPublish(publishedPlan, plan))
            {
                publishedPlan = plan;
                publishedAvailablePlan = current.LastAvailablePlan;
            }
            return ReferenceEquals(current.Plan, publishedPlan) && ReferenceEquals(current.LastAvailablePlan, publishedAvailablePlan)
                ? current : current with { Plan = publishedPlan, LastAvailablePlan = publishedAvailablePlan };
        }
    }
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
            var qualified = RetainValues(Qualify(RetainValues(snapshot, current)), current);
            ScheduleQualityCheck(qualified);
            Volatile.Write(ref current, qualified);
            return true;
        }
    }

    public bool Apply(FuturesOptionTickDataV2ReadModel observation)
    {
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
        ArgumentNullException.ThrowIfNull(observation);
        lock (gate)
        {
            if (retired || observation.ValueDate != current.ValueDate) return false;
            var index = TradeRealtimeSnapshotCalculations.FindLegIndex(current.Legs, observation.ContractId);
            if (index < 0) return false;
            var leg = current.Legs[index];
            if (observation.IsTradeObservation)
            {
                Publish(current with { Legs = current.Legs.SetItem(index, leg with
                {
                    LastTradePrice = observation.OptionPrice, LastTradeSequence = observation.TickId,
                    LastTradeAtUtc = observation.ObservedAtUtc
                }) });
                return true;
            }
            // The option ingress owns validation; the cache applies every matching arrival.
            Publish(current with { Legs = current.Legs.SetItem(index, leg with
            {
                Observation = observation,
                LastValidRisk = StrategyPositionLegOptionData.HasValidRisk(observation) ? observation : leg.LastValidRisk
            }) });
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
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
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
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
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
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
        lock (gate)
        {
            if (retired || clock.GetUtcNow() < nextQualityCheck) return false;
            var next = Qualify(current);
            ScheduleQualityCheck(next);
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
        Legs = snapshot.Legs.Select(leg =>
        {
            var mark = position.Legs.Single(item => item.TradeLegId == leg.Definition.TradeLegId);
            return leg with { Position = mark, Observation = mark.OptionTick ?? leg.Observation,
                LastValidRisk = mark.OptionRisk is {} risk && StrategyPositionLegOptionData.HasValidRisk(risk) ? risk : leg.LastValidRisk };
        }).ToImmutableArray()
    };

    TradeRealtimeSnapshot Qualify(TradeRealtimeSnapshot snapshot) => TradeRealtimeSnapshotCalculations.Qualify(snapshot, clock.GetUtcNow());

    void ScheduleQualityCheck(TradeRealtimeSnapshot snapshot)
    {
        var next = DateTimeOffset.MaxValue;
        foreach (var leg in snapshot.Legs)
            if (leg.QuoteStatus != "Stale" && leg.Observation is {} quote && quote.ValidUntilUtc != default && quote.ValidUntilUtc < next)
                next = quote.ValidUntilUtc;
        if (snapshot.PlanStatus == "Available" && snapshot.Plan?.IronCondorTradePlanSnapshot?.RiskValidUntilUtc is {} riskUntil
            && riskUntil < next.UtcDateTime) next = new DateTimeOffset(DateTime.SpecifyKind(riskUntil, DateTimeKind.Utc));
        nextQualityCheck = next;
    }

    /// <summary>Retains known numeric display values without changing current quote or plan quality metadata.</summary>
    static TradeRealtimeSnapshot RetainValues(TradeRealtimeSnapshot next, TradeRealtimeSnapshot previous)
    {
        if (next.TradeId != previous.TradeId || next.ValueDate != previous.ValueDate
            || next.PositionId != previous.PositionId || next.CompositionVersion != previous.CompositionVersion
            || !SamePositionQuantities(next, previous)) return next;
        var summary = next.Summary; var prior = previous.Summary;
        var plan = next.Plan ?? previous.Plan;
        return next with
        {
            Summary = summary with { UnderlyingPrice = summary.UnderlyingPrice ?? prior.UnderlyingPrice,
                NetDelta = summary.NetDelta ?? prior.NetDelta, NetGamma = summary.NetGamma ?? prior.NetGamma,
                NetVega = summary.NetVega ?? prior.NetVega, NetTheta = summary.NetTheta ?? prior.NetTheta,
                NetRho = summary.NetRho ?? prior.NetRho, PositionValue = summary.PositionValue ?? prior.PositionValue,
                DailyPnlPoints = summary.DailyPnlPoints ?? prior.DailyPnlPoints, NetSpread = summary.NetSpread ?? prior.NetSpread },
            Position = next.Position ?? previous.Position,
            Plan = plan,
            LastAvailablePlan = plan?.IronCondorTradePlanSnapshot?.IsComplete == true ? null
                : next.LastAvailablePlan ?? (previous.Plan?.IronCondorTradePlanSnapshot?.IsComplete == true ? previous.Plan : previous.LastAvailablePlan),
            Legs = next.Legs.Select(leg =>
            {
                var priorLeg = previous.Legs.FirstOrDefault(old => old.Definition.TradeLegId == leg.Definition.TradeLegId
                    && old.Definition.ContractId == leg.Definition.ContractId);
                return leg with { Observation = leg.Observation ?? priorLeg?.Observation,
                    Position = leg.Position ?? priorLeg?.Position,
                    LastValidRisk = leg.LastValidRisk
                        ?? (leg.Observation is {} quote && StrategyPositionLegOptionData.HasValidRisk(quote) ? quote : null)
                        ?? leg.Position?.OptionRisk ?? priorLeg?.LastValidRisk,
                    LastTradePrice = leg.LastTradePrice ?? priorLeg?.LastTradePrice,
                    LastTradeSequence = leg.LastTradePrice.HasValue ? leg.LastTradeSequence : priorLeg?.LastTradeSequence ?? 0,
                    LastTradeAtUtc = leg.LastTradePrice.HasValue ? leg.LastTradeAtUtc : priorLeg?.LastTradeAtUtc ?? default };
            }).ToImmutableArray(),
            Spreads = RetainSpreads(next.Spreads, previous.Spreads)
        };
    }

    // A broker fill changes the quantity basis; values for a different financial composition are not reusable.
    static bool SamePositionQuantities(TradeRealtimeSnapshot next, TradeRealtimeSnapshot previous) =>
        next.Position is not {} position || previous.Position is not {} prior
        || position.Legs.Length == prior.Legs.Length && position.Legs.All(leg => prior.Legs.Any(old =>
            old.TradeLegId == leg.TradeLegId && old.ContractId == leg.ContractId && old.SignedQuantity == leg.SignedQuantity));

    static ImmutableArray<TradeSpreadRealtimeSnapshot> RetainSpreads(ImmutableArray<TradeSpreadRealtimeSnapshot> next,
        ImmutableArray<TradeSpreadRealtimeSnapshot> previous) => next.Select(spread =>
    {
        var prior = previous.FirstOrDefault(old => old.PutCall == spread.PutCall);
        return prior is null ? spread : spread with { NetSpread = spread.NetSpread ?? prior.NetSpread,
            TradeValue = spread.TradeValue ?? prior.TradeValue, TradePnl = spread.TradePnl ?? prior.TradePnl,
            OtmProbability = spread.OtmProbability ?? prior.OtmProbability, ForwardPrice = spread.ForwardPrice ?? prior.ForwardPrice };
    }).ToImmutableArray();

    void Publish(TradeRealtimeSnapshot next)
    {
        next = RetainValues(Qualify(next), current);
        var spreads = TradeRealtimeSnapshotCalculations.CalculateSpreads(next);
        var published = next with { SnapshotRevision = remote is null ? current.SnapshotRevision + 1 : current.SnapshotRevision,
            QualityRevision = remote is null ? 0 : current.QualityRevision + 1,
            Summary = next.Summary with { NetSpread = spreads[0].NetSpread is {} put && spreads[1].NetSpread is {} call ? put + call : SamePositionQuantities(next, current) ? next.Summary.NetSpread : null },
            PublishedAtUtc = clock.GetUtcNow(), Spreads = RetainSpreads(spreads, SamePositionQuantities(next, current) ? current.Spreads : []) };
        ScheduleQualityCheck(published);
        Volatile.Write(ref current, published);
    }
}
