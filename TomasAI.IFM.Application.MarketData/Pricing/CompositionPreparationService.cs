using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Exact workflow revision and hash of its accepted selection, catalog binding and reservation.</summary>
[MessagePackObject]
public sealed record CompositionPreparationKey([property: Key(0)] Guid WorkflowId,
    [property: Key(1)] long InputRevision, [property: Key(2)] string InputSha256);

/// <summary>Immutable market request and its accepted evidence; never refreshed on replay.</summary>
[MessagePackObject]
public sealed record CompositionPreparation([property: Key(0)] int SchemaVersion,
    [property: Key(1)] CompositionPreparationKey Key, [property: Key(2)] string Dataset,
    [property: Key(3)] CompositionSnapshotRequest Request, [property: Key(4)] MarketCompositionSnapshot Snapshot,
    [property: Key(5)] DateTimeOffset PreparedAtUtc, [property: Key(6)] string Digest,
    [property: Key(7)] WorkerOptionChainRelease? DiscoveryLease = null);

public interface ICompositionPreparationStore
{
    Task<CompositionPreparation?> ReadAsync(CompositionPreparationKey key, CancellationToken cancellationToken);
    /// <summary>Atomically saves the first capture. A racing caller receives that winner; conflicting workflow inputs fail.</summary>
    Task<CompositionPreparation> CommitAsync(CompositionPreparation proposed, CancellationToken cancellationToken);
}

public sealed record CompositionPreparationResult(CompositionPreparation? Preparation, OptionPricingFailure? Failure);

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
            || string.IsNullOrWhiteSpace(scope) || scope.Length > 128) throw new ArgumentException("Invalid complete-empty preparation scope.");
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
            || !key.InputSha256.All(Uri.IsHexDigit)) throw new ArgumentException("Exact workflow preparation identity is required.");
    }

    public static void Validate(CompositionPreparation value)
    {
        ValidateKey(value.Key);
        var request = value.Request; var snapshot = value.Snapshot;
        if (value.SchemaVersion is not (1 or 2) || value.Dataset != "GLBX.MDP3" || snapshot.SchemaVersion != 1
            || value.PreparedAtUtc.Offset != TimeSpan.Zero || value.PreparedAtUtc < snapshot.EvaluatedAtUtc
            || value.PreparedAtUtc >= snapshot.ValidUntilUtc || request.SnapshotId != snapshot.SnapshotId
            || request.ScopeId != snapshot.ScopeId || request.GenerationId != snapshot.GenerationId
            || request.Horizon != snapshot.Horizon || request.EvaluatedAtUtc != snapshot.EvaluatedAtUtc
            || snapshot.ValidUntilUtc > request.DeadlineUtc
            || value.DiscoveryLease is { } lease && (lease.ScopeId != request.ScopeId || lease.GenerationId != request.GenerationId
                || lease.LeaseId == Guid.Empty || lease.Ownership is not null || !request.IncludeOptions)
            || snapshot.Digest != PricingSemanticHash.Compute(snapshot with { Digest = "" })
            || !ValidDigest(value)
            || MessagePackBinarySerializer.MeasureContent(value) > 600 * 1024)
            throw new InvalidDataException("Composition preparation identity, hash or bounds are invalid.");
    }

    static bool ValidDigest(CompositionPreparation value)
    {
        if (value.Digest == PricingSemanticHash.Compute(value with { Digest = "" })) return true;
        // Schema 1 preceded DiscoveryLease. Retain the original hash on historical evidence;
        // appending a null property must not invalidate a previously committed capture.
        return value.SchemaVersion == 1 && value.DiscoveryLease is null
            && value.Digest == PricingSemanticHash.Compute(new
            { value.SchemaVersion, value.Key, value.Dataset, value.Request, value.Snapshot, value.PreparedAtUtc, Digest = "" });
    }
}
