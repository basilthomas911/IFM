using System.Collections.Immutable;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>Client-only identity/freshness mirror. Financial numbers are copied from the API without calculation.</summary>
public sealed class TradeRealtimeClientMirror
{
    readonly object gate = new();
    readonly TimeProvider clock;
    readonly HashSet<Guid> retired = [];
    readonly Queue<Guid> retiredOrder = [];
    TradeRealtimeSnapshot current;
    TradeRealtimeSnapshot? remote;

    public TradeRealtimeClientMirror(EstablishedTradeDefinition trade, DateOnly valueDate, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(trade);
        clock = time ?? TimeProvider.System;
        current = new() { TradeId = trade.Id, PositionId = StrategyPositionId.Create(trade.Id, trade.StrategyKind),
            ValueDate = valueDate, CompositionVersion = trade.EvidenceRevision,
            Legs = trade.Legs.Select(l => new TradeLegRealtimeSnapshot { Definition = l }).ToImmutableArray() };
    }

    public TradeRealtimeSnapshot Read() => Volatile.Read(ref current);

    public bool AcceptSnapshot(TradeRealtimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (gate)
        {
            if (snapshot.SchemaVersion != 1 || snapshot.TradeId != current.TradeId || snapshot.ValueDate != current.ValueDate
                || snapshot.PositionId != current.PositionId || snapshot.MonitoringGeneration == Guid.Empty || snapshot.SnapshotRevision < 0
                || snapshot.CompositionVersion < current.CompositionVersion || retired.Contains(snapshot.MonitoringGeneration)
                || snapshot.Legs.Length is < 1 or > 64 || snapshot.Legs.Select(l => l.Definition.TradeLegId).Distinct().Count() != snapshot.Legs.Length
                || snapshot.CompositionVersion == current.CompositionVersion && (snapshot.Legs.Length != current.Legs.Length
                    || snapshot.Legs.Any(l => !current.Legs.Any(expected => expected.Definition.TradeLegId == l.Definition.TradeLegId
                        && expected.Definition.ContractId == l.Definition.ContractId)))) return false;
            if (remote is { } previous && (snapshot.MonitoringGeneration == previous.MonitoringGeneration
                ? snapshot.SnapshotRevision <= previous.SnapshotRevision : snapshot.PublishedAtUtc <= previous.PublishedAtUtc)) return false;
            if (remote is { } prior && prior.MonitoringGeneration != snapshot.MonitoringGeneration)
            {
                retired.Add(prior.MonitoringGeneration); retiredOrder.Enqueue(prior.MonitoringGeneration);
                if (retiredOrder.Count > 128) retired.Remove(retiredOrder.Dequeue());
            }
            remote = snapshot;
            Volatile.Write(ref current, WithFreshness(snapshot));
            return true;
        }
    }

    public bool Expire()
    {
        lock (gate)
        {
            if (remote is null) return false;
            var next = WithFreshness(current);
            if (ReferenceEquals(next, current)) return false;
            Volatile.Write(ref current, next); return true;
        }
    }

    TradeRealtimeSnapshot WithFreshness(TradeRealtimeSnapshot snapshot)
    {
        var now = clock.GetUtcNow();
        ImmutableArray<TradeLegRealtimeSnapshot>.Builder? changed = null;
        for (var i = 0; i < snapshot.Legs.Length; i++)
        {
            var leg = snapshot.Legs[i];
            if (leg.Observation is not { ValidUntilUtc: var until } || until == default || until > now
                || leg.QuoteStatus == "Stale" && leg.PricingStatus == "Stale") continue;
            changed ??= snapshot.Legs.ToBuilder();
            changed[i] = leg with { QuoteStatus = "Stale", PricingStatus = "Stale" };
        }
        var expiredPlan = snapshot.PlanStatus == "Available"
            && snapshot.Plan?.IronCondorTradePlanSnapshot?.RiskValidUntilUtc is { } deadline && deadline <= now.UtcDateTime;
        if (changed is null && !expiredPlan) return snapshot;
        return snapshot with { Legs = changed?.ToImmutable() ?? snapshot.Legs,
            Status = changed is null ? snapshot.Status : "Stale", PlanStatus = expiredPlan ? "Stale" : snapshot.PlanStatus,
            QualityRevision = snapshot.QualityRevision + 1 };
    }
}
