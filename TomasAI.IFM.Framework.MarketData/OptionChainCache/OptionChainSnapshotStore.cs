using System.Collections.Immutable;

namespace TomasAI.IFM.Framework.MarketData.OptionChainCache;

/// <summary>A prepared immutable payload and its monotonically increasing publication version.</summary>
public sealed record OptionChainPublication<T>(long Version, DateTimeOffset PublishedAtUtc, T Snapshot) where T : class;

/// <summary>Provider-independent generation-fenced snapshot storage. Payloads must be deeply immutable.</summary>
public sealed class OptionChainSnapshotStore<T> where T : class
{
    sealed record Epoch(Guid Generation, bool Admitted, ImmutableDictionary<string, OptionChainPublication<T>> Scopes);
    Epoch epoch = new(Guid.Empty, false, ImmutableDictionary<string, OptionChainPublication<T>>.Empty);
    readonly int maximumScopes;

    /// <summary>Creates a bounded store; publication allocates off the consumer path.</summary>
    public OptionChainSnapshotStore(int maximumScopes = 128)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumScopes);
        this.maximumScopes = maximumScopes;
    }

    /// <summary>Admits a fresh generation and discards all previous-generation payloads.</summary>
    public void Admit(Guid generation)
    {
        if (generation == Guid.Empty) throw new ArgumentException("A generation is required.", nameof(generation));
        while (true)
        {
            var prior = Volatile.Read(ref epoch);
            if (prior.Generation == generation && prior.Admitted) return;
            var next = new Epoch(generation, true, ImmutableDictionary<string, OptionChainPublication<T>>.Empty);
            if (ReferenceEquals(Interlocked.CompareExchange(ref epoch, next, prior), prior)) return;
        }
    }

    /// <summary>Fences only the specified generation; a late old-generation failure cannot fence its replacement.</summary>
    public void Fence(Guid generation)
    {
        while (true)
        {
            var prior = Volatile.Read(ref epoch);
            if (prior.Generation != generation || !prior.Admitted) return;
            var next = prior with { Admitted = false, Scopes = prior.Scopes.Clear() };
            if (ReferenceEquals(Interlocked.CompareExchange(ref epoch, next, prior), prior)) return;
        }
    }

    /// <summary>Publishes a prepared payload if its generation and increasing version are current.</summary>
    public bool Publish(Guid generation, string scope, OptionChainPublication<T> publication)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(publication.Snapshot);
        if (publication.Version <= 0 || publication.PublishedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Positive version and UTC timestamp are required.", nameof(publication));
        while (true)
        {
            var prior = Volatile.Read(ref epoch);
            if (!prior.Admitted || prior.Generation != generation) return false;
            if (prior.Scopes.TryGetValue(scope, out var existing))
            {
                if (existing.Version >= publication.Version || existing.PublishedAtUtc > publication.PublishedAtUtc) return false;
            }
            else if (prior.Scopes.Count >= maximumScopes) return false;
            var next = prior with { Scopes = prior.Scopes.SetItem(scope, publication) };
            if (ReferenceEquals(Interlocked.CompareExchange(ref epoch, next, prior), prior)) return true;
        }
    }

    /// <summary>Removes one retired lookup scope without affecting another owner's generation.</summary>
    public void Remove(Guid generation, string scope)
    {
        while (true)
        {
            var prior = Volatile.Read(ref epoch);
            if (prior.Generation != generation || !prior.Scopes.ContainsKey(scope)) return;
            var next = prior with { Scopes = prior.Scopes.Remove(scope) };
            if (ReferenceEquals(Interlocked.CompareExchange(ref epoch, next, prior), prior)) return;
        }
    }

    /// <summary>Reads one immutable epoch reference without waiting, retrying or allocating.</summary>
    public OptionChainPublication<T>? Read(Guid generation, string scope)
    {
        var current = Volatile.Read(ref epoch);
        return current.Admitted && (generation == Guid.Empty || current.Generation == generation) && current.Scopes.TryGetValue(scope, out var value)
            ? value : null;
    }
}
