using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;

/// <summary>Native workload proof supplements owner replacement; it never replaces
/// loaded-trade leg, risk, plan or publication verification.</summary>
public static class OptionRecoveryWorkloadProof
{
    public static bool IsComplete(MarketDataReadSnapshot snapshot, MarketDataDatasetStatus expected,
        DateTimeOffset started, DateTimeOffset now)
    {
        var status = snapshot.Status;
        var evidence = snapshot.NativeOptionWorkload;
        var current = status.Datasets.SingleOrDefault(x => x.Role == expected.Role && x.Dataset == expected.Dataset);
        if (status.RecoveryOwnership != MarketDataRecoveryOwnership.TokioDatasetOwner || !status.Ready
            || status.HostInstanceId == Guid.Empty || status.ValueDate != expected.ValueDate || status.OptionProcessingFailure is not null
            || status.EnabledOptionTrades is not { Length: 0 }
            || current is null || !current.Running || !current.Healthy || current.Lifecycle != "Healthy"
            || current.WorkerInstanceId != expected.WorkerInstanceId || current.GenerationId != expected.GenerationId
            || current.ProcessId != expected.ProcessId || current.ValueDate != expected.ValueDate
            || evidence is null || evidence.GenerationId != expected.GenerationId || evidence.ReplacementPending
            || evidence.ObservedAtUtc < started || evidence.ObservedAtUtc > now
            || evidence.Scopes is null || evidence.ReadyScopes is null || evidence.OptionScopes is null
            || evidence.ReadyScopes.Length!=evidence.Scopes.Length
            || evidence.Scopes.Any(string.IsNullOrWhiteSpace)
            || evidence.OptionScopes.Length!=evidence.Scopes.Count(x=>!x.StartsWith("underlying-",StringComparison.Ordinal))
            || evidence.FormatVersion is <0 or >1
            || evidence.Scopes.Length > (evidence.FormatVersion==1?256:64)
            || evidence.Scopes.Distinct(StringComparer.Ordinal).Count() != evidence.Scopes.Length
            || !evidence.Scopes.ToHashSet(StringComparer.Ordinal).SetEquals(evidence.ReadyScopes)
            || !evidence.OptionScopes.ToHashSet(StringComparer.Ordinal).SetEquals(
                evidence.Scopes.Where(x => !x.StartsWith("underlying-", StringComparison.Ordinal)))) return false;
        // The complete native owner attests the same original-episode proofs
        // used by its supervisor. Financial candidate availability is independent
        // of transport recovery. This method still rejects any enabled/unknown
        // loaded-trade workload; its per-leg/risk/plan checks run separately.
        if(evidence.FormatVersion==1)return evidence.NativeResourceProofsComplete;
        // All subscriptions must be ready. Cache policy intentionally publishes
        // preferred eligible expiries only, so unused fallback scopes need not
        // have a selected publication. Prove the downstream cache path using a
        // current qualified publication belonging to an actually owned scope.
        return evidence.OptionScopes.Length == 0 || (snapshot.OptionChains ?? []).Any(publication =>
            publication.ValueDate == expected.ValueDate && publication.Version > 0
            && evidence.OptionScopes.Contains(publication.Snapshot.ScopeId, StringComparer.Ordinal)
            && publication.Snapshot.GenerationId == expected.GenerationId
            && publication.Snapshot.EvaluatedAtUtc >= started && publication.Snapshot.EvaluatedAtUtc <= now
            && publication.Snapshot.ValidUntilUtc > now && !publication.Snapshot.Instruments.IsDefaultOrEmpty);
    }
}
