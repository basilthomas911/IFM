using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

/// <summary>Bounded per-monitor whole-trade read model. Serialized writes; atomic, non-consuming reads.</summary>
public sealed class TradeRealtimeHotCache
{
    readonly object gate = new();
    readonly HashSet<Guid> retiredGenerations = [];
    readonly HashSet<Guid> retiredMonitorGenerations = [];
    readonly Queue<Guid> retiredSourceOrder = new(), retiredMonitorOrder = new();
    readonly TimeProvider clock;
    TradeRealtimeSnapshot current;
    TradeRealtimeSnapshot? remote;
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
            var qualified = Qualify(snapshot);
            ScheduleQualityCheck(qualified);
            Volatile.Write(ref current, qualified);
            return true;
        }
    }

    public bool Apply(FuturesOptionTickDataV2ReadModel observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        lock (gate)
        {
            if (retired || observation.ValueDate != current.ValueDate) return false;
            var index = TradeRealtimeSnapshotCalculations.FindLegIndex(current.Legs, observation.ContractId);
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
        Legs = snapshot.Legs.Select(leg => leg with { Position = position.Legs.Single(mark => mark.TradeLegId == leg.Definition.TradeLegId) }).ToImmutableArray()
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

    void Publish(TradeRealtimeSnapshot next)
    {
        next = Qualify(next);
        var spreads = TradeRealtimeSnapshotCalculations.CalculateSpreads(next);
        var published = next with { SnapshotRevision = remote is null ? current.SnapshotRevision + 1 : current.SnapshotRevision,
            QualityRevision = remote is null ? 0 : current.QualityRevision + 1,
            Summary = next.Summary with { NetSpread = spreads[0].NetSpread is {} put && spreads[1].NetSpread is {} call ? put + call : null },
            PublishedAtUtc = clock.GetUtcNow(), Spreads = spreads };
        ScheduleQualityCheck(published);
        Volatile.Write(ref current, published);
    }
}
