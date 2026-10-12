using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>
/// Host-owned desired subscriptions. Only the current value date is retained for each dataset,
/// so a long-running host cannot accumulate historical manifests. Revisions remain monotonic
/// across date changes; delayed acknowledgements and writes cannot resurrect old desired state.
/// </summary>
public sealed class DatasetDesiredSubscriptionRegistry
{
    public const int MaximumDatasets = 16;
    readonly System.Threading.Lock gate = new();
    readonly Dictionary<string, DatasetSubscriptionManifest> manifests = new(StringComparer.Ordinal);
    readonly Dictionary<string, DatasetSubscriptionContract[]> core = new(StringComparer.Ordinal);
    readonly Dictionary<string, DatasetSubscriptionContract[]> durable = new(StringComparer.Ordinal);

    /// <summary>Atomically captures required datasets, revisions, mappings and capture time without external I/O.</summary>
    public DatasetRecoveryManifestSnapshot CaptureRecoverySnapshot(DateOnly valueDate,
        IReadOnlyCollection<string> requiredDatasets, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(requiredDatasets);
        if (valueDate == default || requiredDatasets.Count is < 1 or > MaximumDatasets)
            throw new ArgumentException("Recovery requires a value date and bounded required dataset set.",nameof(valueDate));
        lock (gate)
        {
            var unique = new HashSet<string>(requiredDatasets, StringComparer.Ordinal);
            if (unique.Count != requiredDatasets.Count)
                throw new ArgumentException("Required datasets must be unique.", nameof(requiredDatasets));
            var result = new DatasetSubscriptionManifest[unique.Count];
            var index = 0;
            foreach (var dataset in unique.Order(StringComparer.Ordinal))
            {
                DatasetSubscriptionManifest.ValidateText(dataset, 64, nameof(requiredDatasets));
                if (!manifests.TryGetValue(dataset, out var manifest) || manifest.ValueDate != valueDate)
                    throw new InvalidOperationException($"No current recovery manifest for {dataset} on {valueDate}.");
                result[index++] = manifest;
            }
            return new DatasetRecoveryManifestSnapshot(valueDate, timeProvider.GetUtcNow(), result);
        }
    }

    /// <summary>Captures every current dataset for the active value date under one registry lock.</summary>
    public DatasetRecoveryManifestSnapshot CaptureCurrentRecoverySnapshot(DateOnly valueDate,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfEqual(valueDate, default);
        lock (gate)
        {
            var current = manifests.Values.Where(item => item.ValueDate == valueDate).ToArray();
            return new DatasetRecoveryManifestSnapshot(valueDate, timeProvider.GetUtcNow(), current);
        }
    }

    /// <summary>Captures a complete value-date-matched local recovery set without external I/O.</summary>
    public IReadOnlyList<DatasetSubscriptionManifest> CaptureRecoverySet(
        DateOnly valueDate, IReadOnlyCollection<string> requiredDatasets)
    {
        return CaptureRecoverySnapshot(valueDate, requiredDatasets, TimeProvider.System).Manifests;
    }

    public DatasetSubscriptionManifest Set(
        string dataset,
        DateOnly valueDate,
        IEnumerable<DatabentoContractRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var bounded = registrations.Take(DatasetSubscriptionManifest.MaximumContracts + 1)
            .Select(DatasetSubscriptionContract.FromRegistration).ToArray();
        var candidate = new DatasetSubscriptionManifest(dataset, valueDate, 1, bounded);
        lock (gate)
        {
            var original = candidate.Contracts.ToArray();
            candidate = new DatasetSubscriptionManifest(dataset, valueDate, 1,
                Merge(original, durable.GetValueOrDefault(dataset) ?? []));
            if (manifests.TryGetValue(dataset, out var current))
            {
                if (valueDate < current.ValueDate)
                    throw new InvalidOperationException("A past value date cannot replace a dataset's current desired subscriptions.");
                if (valueDate == current.ValueDate && current.Contracts.SequenceEqual(candidate.Contracts))
                {
                    core[dataset] = original;
                    return current;
                }
                candidate = new DatasetSubscriptionManifest(dataset, valueDate,
                    checked(current.Revision + 1), candidate.Contracts);
            }
            else if (manifests.Count >= MaximumDatasets)
                throw new InvalidOperationException("The desired dataset registry has reached its bounded capacity.");

            manifests[dataset] = candidate;
            core[dataset] = original;
            return candidate;
        }
    }

    /// <summary>Atomically overlays committed dependencies without replacing concurrent rollover roles.</summary>
    public DatasetSubscriptionManifest SetDurable(string dataset, DateOnly valueDate,
        IReadOnlyList<DatasetSubscriptionContract> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        if (registrations.Count > DatasetSubscriptionManifest.MaximumContracts
            || registrations.Any(x => x.OnTheRun || x.Rollover || !string.Equals(x.Dataset,dataset,StringComparison.Ordinal)))
            throw new ArgumentException("Durable dependencies cannot assign core futures roles.",nameof(registrations));
        var copy = registrations.ToArray();
        lock (gate)
        {
            if (!manifests.TryGetValue(dataset, out var current) || current.ValueDate != valueDate)
                throw new InvalidOperationException("Current native manifest is unavailable.");
            var merged = Merge(core[dataset], copy);
            var candidate = new DatasetSubscriptionManifest(dataset, valueDate, checked(current.Revision + 1), merged);
            durable[dataset] = copy;
            if (current.Contracts.SequenceEqual(candidate.Contracts)) return current;
            return manifests[dataset] = candidate;
        }
    }

    static DatasetSubscriptionContract[] Merge(IReadOnlyList<DatasetSubscriptionContract> baseline,
        IReadOnlyList<DatasetSubscriptionContract> pinned)
    {
        var result = baseline.ToDictionary(x => x.DomainContractId, StringComparer.Ordinal);
        foreach (var item in pinned)
        {
            if (result.TryGetValue(item.DomainContractId, out var existing))
            {
                if ((existing with { OnTheRun = false, Rollover = false }) != item)
                    throw new InvalidDataException("Durable and core provider mappings conflict.");
            }
            else result.Add(item.DomainContractId, item);
        }
        return result.Values.OrderBy(x => x.DomainContractId, StringComparer.Ordinal).ToArray();
    }

    public bool TryGet(string dataset, DateOnly valueDate, out DatasetSubscriptionManifest manifest)
    {
        DatasetSubscriptionManifest.ValidateText(dataset, 64, nameof(dataset));
        lock (gate)
        {
            if (manifests.TryGetValue(dataset, out var current) && current.ValueDate == valueDate)
            {
                manifest = current;
                return true;
            }
            manifest = null!;
            return false;
        }
    }

    public bool IsCurrent(DatasetSubscriptionManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        lock (gate) return IsCurrentUnderLock(manifest);
    }

    /// <summary>
    /// Runs the small, synchronous admission action only while the supplied complete desired
    /// state remains current. The action must not block, perform I/O or mutate this registry.
    /// </summary>
    public bool TryWithCurrent(DatasetSubscriptionManifest manifest, Action action)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(action);
        lock (gate)
        {
            if (!IsCurrentUnderLock(manifest)) return false;
            action();
            return true;
        }
    }

    public IReadOnlyList<DatasetSubscriptionManifest> Snapshot()
    {
        lock (gate)
            return Array.AsReadOnly(manifests.Values.OrderBy(static item => item.Dataset,
                StringComparer.Ordinal).ToArray());
    }

    bool IsCurrentUnderLock(DatasetSubscriptionManifest manifest) =>
        manifests.TryGetValue(manifest.Dataset, out var current)
        && current.ValueDate == manifest.ValueDate
        && current.Revision == manifest.Revision
        && string.Equals(current.Fingerprint, manifest.Fingerprint, StringComparison.Ordinal);
}
