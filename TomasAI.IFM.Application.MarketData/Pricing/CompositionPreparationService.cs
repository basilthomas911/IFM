using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Freezes market evidence before workflow acceptance/dispatch. Storage faults never dispatch an uncommitted capture.</summary>
public sealed class CompositionPreparationService(ICompositionMarketDataApi market, ICompositionPreparationStore store, TimeProvider? time = null)
{
    readonly TimeProvider clock = time ?? TimeProvider.System;

    /// <summary>Freezes an authoritative complete-empty qualified option universe; no quote, rate or native feed is invented.</summary>
    public async Task<CompositionPreparationResult> PrepareEmptyAsync(CompositionPreparationKey key, string scope,
        string horizon, Guid generation, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        var existing = await store.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing is not null) return Replay(existing);
        var at = clock.GetUtcNow();
        if (generation == Guid.Empty || horizon is not ("Daily" or "Weekly" or "Monthly") || deadline <= at
            || string.IsNullOrWhiteSpace(scope) || scope.Length > 128) throw new ArgumentException("Invalid complete-empty preparation scope.",nameof(key));
        var request = new CompositionSnapshotRequest(Guid.NewGuid(), scope, horizon, generation, at, deadline, true);
        var snapshot = new MarketCompositionSnapshot(1, request.SnapshotId, scope, scope, horizon, generation, at,
            deadline < at.AddSeconds(5) ? deadline : at.AddSeconds(5), [], "");
        snapshot = snapshot with { Digest = PricingSemanticHash.Compute(snapshot) };
        var prepared = new CompositionPreparation(2, key, "GLBX.MDP3", request, snapshot, at, "");
        prepared = prepared with { Digest = PricingSemanticHash.Compute(prepared) };
        Validate(prepared);
        return Replay(await store.CommitAsync(prepared, cancellationToken).ConfigureAwait(false));
    }

    public async Task<CompositionPreparationResult> PrepareAsync(CompositionPreparationKey key, string dataset,
        CompositionSnapshotRequest request, CancellationToken cancellationToken, WorkerOptionChainRelease? discoveryLease = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKey(key);
        var existing = await store.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing is not null) return Replay(existing);
        CompositionSnapshotResult result;
        try { result = await market.CaptureAsync(dataset, request, cancellationToken).ConfigureAwait(false); }
        catch (CompositionMarketSourceException ex) { return new(null, new(ex.Code, "Worker", "", "The admitted dataset is unavailable.")); }
        if (result.Failure is not null) return new(null, result.Failure);
        if (result.Snapshot is null) return new(null, new("SnapshotUnavailable", "Snapshot", "", "No complete market snapshot was returned."));
        if (request.EvaluatedAtUtc == default) request = request with { EvaluatedAtUtc = result.Snapshot.EvaluatedAtUtc };
        var prepared = new CompositionPreparation(2, key, dataset, request, result.Snapshot, clock.GetUtcNow(), "", discoveryLease);
        prepared = prepared with { Digest = PricingSemanticHash.Compute(prepared) };
        Validate(prepared);
        cancellationToken.ThrowIfCancellationRequested();
        if (clock.GetUtcNow() >= prepared.Snapshot.ValidUntilUtc) return Expired();
        var committed = await store.CommitAsync(prepared, cancellationToken).ConfigureAwait(false);
        return Replay(committed);
    }

    /// <summary>Freezes an immediately available cache result without calling the provider. Persistence remains a separate acceptance boundary.</summary>
    public async Task<CompositionPreparationResult> PrepareCachedAsync(CompositionPreparationKey key,
        TomasAI.IFM.Application.MarketData.Contracts.IOptionChainCache cache,
        TomasAI.IFM.Application.MarketData.Contracts.OptionChainSnapshotRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(cache);
        var existing = await store.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing is not null) return Replay(existing);
        var result = cache.TryGetSnapshot(request);
        if (!result.IsReady || result.Snapshot is not { } snapshot)
            return new(null, new(result.ReasonCode, "OptionChainCache", "", result.Outcome.ToString()));
        // Choose a new acceptance identity without mutating the resident cache snapshot or a previously accepted execution.
        var until = snapshot.ValidUntilUtc < request.DeadlineUtc ? snapshot.ValidUntilUtc : request.DeadlineUtc;
        snapshot = snapshot with { SnapshotId = Guid.NewGuid(), ValidUntilUtc = until, Digest = "" };
        snapshot = snapshot with { Digest = PricingSemanticHash.Compute(snapshot) };
        var capture = new CompositionSnapshotRequest(snapshot.SnapshotId, snapshot.ScopeId, snapshot.Horizon,
            snapshot.GenerationId, snapshot.EvaluatedAtUtc, request.DeadlineUtc, true,
            MaximumQuoteAgeMilliseconds: request.MaximumQuoteAgeMilliseconds, MaximumQuoteSkewMilliseconds: request.MaximumQuoteSkewMilliseconds);
        var proposed = new CompositionPreparation(2, key, "GLBX.MDP3", capture, snapshot, clock.GetUtcNow(), "");
        proposed = proposed with { Digest = PricingSemanticHash.Compute(proposed) };
        if (clock.GetUtcNow() >= snapshot.ValidUntilUtc) return Expired();
        Validate(proposed);
        return Replay(await store.CommitAsync(proposed, cancellationToken).ConfigureAwait(false));
    }

    CompositionPreparationResult Replay(CompositionPreparation preparation)
    {
        Validate(preparation);
        return clock.GetUtcNow() >= preparation.Snapshot.ValidUntilUtc ? Expired() : new(preparation, null);
    }
    static CompositionPreparationResult Expired() => new(null, new("AcceptedSnapshotExpired", "Snapshot", "",
        "The frozen snapshot expired. This workflow revision cannot recapture market evidence."));

    public static void ValidateKey(CompositionPreparationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.WorkflowId == Guid.Empty || key.InputRevision < 1 || key.InputSha256 is not { Length: 64 }
            || !key.InputSha256.All(Uri.IsHexDigit)) throw new ArgumentException("Exact workflow preparation identity is required.",nameof(key));
    }

    public static void Validate(CompositionPreparation value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateKey(value.Key);
        var request = value.Request; var snapshot = value.Snapshot;
        if (value.SchemaVersion is not (1 or 2) || !string.Equals(value.Dataset,"GLBX.MDP3",StringComparison.Ordinal) || snapshot.SchemaVersion != 1
            || value.PreparedAtUtc.Offset != TimeSpan.Zero || value.PreparedAtUtc < snapshot.EvaluatedAtUtc
            || value.PreparedAtUtc >= snapshot.ValidUntilUtc || request.SnapshotId != snapshot.SnapshotId
            || !string.Equals(request.ScopeId,snapshot.ScopeId,StringComparison.Ordinal) || request.GenerationId != snapshot.GenerationId
            || !string.Equals(request.Horizon,snapshot.Horizon,StringComparison.Ordinal) || request.EvaluatedAtUtc != snapshot.EvaluatedAtUtc
            || snapshot.ValidUntilUtc > request.DeadlineUtc
            || value.DiscoveryLease is { } lease && (!string.Equals(lease.ScopeId,request.ScopeId,StringComparison.Ordinal) || lease.GenerationId != request.GenerationId
                || lease.LeaseId == Guid.Empty || lease.Ownership is not null || !request.IncludeOptions)
            || !string.Equals(snapshot.Digest,PricingSemanticHash.Compute(snapshot with { Digest = "" }),StringComparison.Ordinal)
            || !ValidDigest(value)
            || MessagePackBinarySerializer.MeasureContent(value) > 600 * 1024)
            throw new InvalidDataException("Composition preparation identity, hash or bounds are invalid.");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S125", Justification = "This comment documents the persisted schema-one hash compatibility contract; it is not disabled code.")]
    static bool ValidDigest(CompositionPreparation value)
    {
        if (string.Equals(value.Digest,PricingSemanticHash.Compute(value with { Digest = "" }),StringComparison.Ordinal)) return true;
        // Schema 1 preceded DiscoveryLease. Retain the original hash on historical evidence;
        // appending a null property must not invalidate a previously committed capture.
        return value.SchemaVersion == 1 && value.DiscoveryLease is null
            && string.Equals(value.Digest,PricingSemanticHash.Compute(new
            { value.SchemaVersion, value.Key, value.Dataset, value.Request, value.Snapshot, value.PreparedAtUtc, Digest = "" }),StringComparison.Ordinal);
    }
}
