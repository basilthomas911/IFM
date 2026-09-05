using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>
/// Resolved provider mapping, not a subscription lease. Stage 2 contract authority remains
/// responsible for assigning the fixed futures roles; workers must not reconstruct provider
/// symbols or role flags from a domain contract identifier.
/// </summary>
[MessagePackObject]
public sealed record DatasetSubscriptionContract
{
    [Key(0)] public required string DomainContractId { get; init; }
    [Key(1)] public required string ProviderContractName { get; init; }
    [Key(2)] public required AssetTypeId AssetTypeId { get; init; }
    [Key(3)] public required string RootSymbol { get; init; }
    [Key(4)] public required string Dataset { get; init; }
    [Key(5)] public bool OnTheRun { get; init; }
    [Key(6)] public bool Rollover { get; init; }

    public static DatasetSubscriptionContract FromRegistration(DatabentoContractRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        return new DatasetSubscriptionContract
        {
            DomainContractId = registration.DomainContractId,
            ProviderContractName = registration.ProviderContractName,
            AssetTypeId = registration.AssetTypeId,
            RootSymbol = registration.RootSymbol ?? string.Empty,
            Dataset = registration.Dataset ?? string.Empty,
            OnTheRun = registration.OnTheRun,
            Rollover = registration.Rollover
        };
    }

    public DatabentoContractRegistration ToRegistration() => new()
    {
        DomainContractId = DomainContractId,
        ProviderContractName = ProviderContractName,
        AssetTypeId = AssetTypeId,
        RootSymbol = RootSymbol,
        Dataset = Dataset,
        OnTheRun = OnTheRun,
        Rollover = Rollover
    };
}

/// <summary>
/// An immutable, complete current desired state for one dataset/value date. Canonical ordering
/// makes an exact duplicate independent of the caller's enumeration order. The fingerprint binds
/// every mapping field and the full manifest identity; an acknowledgement of a revision number
/// alone is not sufficient to admit a worker.
/// </summary>
[MessagePackObject]
public sealed class DatasetSubscriptionManifest
{
    public const int MaximumContracts = 16;
    public const int MaximumManifestBytes = 240 * 1024;

    [Key(0)] public string Dataset { get; }
    [Key(1)] public DateOnly ValueDate { get; }
    [Key(2)] public long Revision { get; }
    [Key(3)] public IReadOnlyList<DatasetSubscriptionContract> Contracts { get; }
    [IgnoreMember] public string Fingerprint { get; }

    [SerializationConstructor]
    public DatasetSubscriptionManifest(
        string dataset,
        DateOnly valueDate,
        long revision,
        IReadOnlyList<DatasetSubscriptionContract> contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        if (contracts.Count is < 1 or > MaximumContracts || contracts.Any(static item => item is null))
            throw new ArgumentException("A dataset manifest requires a bounded, non-empty complete registration set.", nameof(contracts));
        Dataset = dataset;
        ValueDate = valueDate;
        Revision = revision;
        Contracts = Array.AsReadOnly(contracts.OrderBy(static item => item.DomainContractId,
            StringComparer.Ordinal).ToArray());
        Validate();
        Fingerprint = ComputeFingerprint();
    }

    public DatasetSubscriptionManifest Validate()
    {
        ValidateText(Dataset, 64, nameof(Dataset));
        if (ValueDate == default || Revision < 1)
            throw new ArgumentException("A dataset manifest requires an active value date and a positive revision.");
        if (Contracts.Count is < 1 or > MaximumContracts)
            throw new ArgumentException("Dataset manifest registration count is outside its bounded capacity.");

        var domains = new HashSet<string>(StringComparer.Ordinal);
        var providers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var contract in Contracts)
        {
            ArgumentNullException.ThrowIfNull(contract);
            ValidateText(contract.DomainContractId, 256, nameof(contract.DomainContractId));
            ValidateText(contract.ProviderContractName, 256, nameof(contract.ProviderContractName));
            ValidateText(contract.RootSymbol, 32, nameof(contract.RootSymbol));
            ValidateText(contract.Dataset, 64, nameof(contract.Dataset));
            if (!string.Equals(contract.Dataset, Dataset, StringComparison.Ordinal))
                throw new ArgumentException("Every resolved registration must belong to the manifest dataset.");
            if (contract.AssetTypeId != AssetTypeId.Futures)
                throw new ArgumentException("Stage 3 manifests accept resolved futures registrations only; option ownership belongs to Stage 4.");
            if (!domains.Add(contract.DomainContractId) || !providers.Add(contract.ProviderContractName))
                throw new ArgumentException("A dataset manifest cannot contain duplicate domain or provider contract identities.");
        }

        // String/count limits already bound allocations. Also reserve space for the surrounding
        // authenticated control envelope inside its 256 KiB frame ceiling.
        if (MessagePackSerializer.Serialize(this).Length > MaximumManifestBytes)
            throw new ArgumentException("The complete dataset manifest exceeds its bounded wire size.");
        return this;
    }

    public IReadOnlyList<DatabentoContractRegistration> GetRegistrations() =>
        Array.AsReadOnly(Contracts.Select(static item => item.ToRegistration()).ToArray());

    internal static void ValidateText(string value, int maximumLength, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsControl))
            throw new ArgumentException("Manifest identifiers must be resolved, bounded and free of control characters or surrounding whitespace.", parameter);
    }

    string ComputeFingerprint()
    {
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, Encoding.UTF8, leaveOpen: true))
        {
            // Length-prefixed strings prevent delimiter ambiguities. Format-version binds future
            // changes without relying on runtime-dependent record hash codes or dictionary order.
            writer.Write(1);
            writer.Write(Dataset);
            writer.Write(ValueDate.DayNumber);
            writer.Write(Revision);
            writer.Write(Contracts.Count);
            foreach (var contract in Contracts)
            {
                writer.Write(contract.DomainContractId);
                writer.Write(contract.ProviderContractName);
                writer.Write((int)contract.AssetTypeId);
                writer.Write(contract.RootSymbol);
                writer.Write(contract.Dataset);
                writer.Write(contract.OnTheRun);
                writer.Write(contract.Rollover);
            }
        }
        return Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length))));
    }
}

/// <summary>
/// Host-owned desired subscriptions. Only the current value date is retained for each dataset,
/// so a long-running host cannot accumulate historical manifests. Revisions remain monotonic
/// across date changes; delayed acknowledgements and writes cannot resurrect old desired state.
/// </summary>
public sealed class DatasetDesiredSubscriptionRegistry
{
    public const int MaximumDatasets = 16;
    readonly object gate = new();
    readonly Dictionary<string, DatasetSubscriptionManifest> manifests = new(StringComparer.Ordinal);

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
            if (manifests.TryGetValue(dataset, out var current))
            {
                if (valueDate < current.ValueDate)
                    throw new InvalidOperationException("A past value date cannot replace a dataset's current desired subscriptions.");
                if (valueDate == current.ValueDate && current.Contracts.SequenceEqual(candidate.Contracts))
                    return current;
                candidate = new DatasetSubscriptionManifest(dataset, valueDate,
                    checked(current.Revision + 1), candidate.Contracts);
            }
            else if (manifests.Count >= MaximumDatasets)
                throw new InvalidOperationException("The desired dataset registry has reached its bounded capacity.");

            manifests[dataset] = candidate;
            return candidate;
        }
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
