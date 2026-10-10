using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.MarketData.OptionChainCache;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>Bounded background coverage observations. Volatility here selects subscriptions, never supplies an execution valuation.</summary>
public sealed class OptionChainCoverageObservations(DatasetWorkerAdmissionRegistry admissions)
{
    sealed record Observation(double Volatility);
    readonly OptionChainSnapshotStore<Observation> store = new(128);
    readonly object gate = new();
    /// <summary>Uses observed maximum qualified IV from the current worker generation; 20% bootstraps an unpriced universe.</summary>
    public double Read(Guid policyId)
    {
        if (!admissions.TryGet("GLBX.MDP3", out var current)) return .20;
        return store.Read(current.GenerationId, policyId.ToString())?.Snapshot.Volatility ?? .20;
    }
    /// <summary>Publishes finite current-generation coverage IV only, fencing prior generation observations.</summary>
    public void Observe(Guid policyId, Guid generation, double volatility, long version, DateTimeOffset observedAt)
    {
        if (policyId == Guid.Empty || !double.IsFinite(volatility) || volatility is <= 0 or > 5) return;
        lock (gate)
        {
            if (!admissions.TryGet("GLBX.MDP3", out var current) || current.GenerationId != generation) return;
            store.Admit(generation);
            store.Publish(generation, policyId.ToString(), new(version, observedAt, new Observation(volatility)));
        }
    }
}
