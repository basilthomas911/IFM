using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

/// <summary>Shared bounded canonicalization for the isolated durable store, not financial validation.</summary>
public static class DurableSubscriptionContract
{
    public const int MaximumAuthorities = 10_000;
    public const int MaximumLeases = 10_000;
    public const int MaximumOwnerLeases = 128;
    public const int MaximumSnapshotBytes = 16 * 1024 * 1024;

    public static void ValidateScope(string scope, string dataset)
    {
        Identity(scope, 128);
        Identity(dataset, 64);
    }

    public static DurableAuthorityMutation Freeze(DurableAuthorityMutation value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateScope(value.Scope, value.Dataset);
        Identity(value.SourceId, 256);
        Identity(value.ReasonCode, 128);
        ValidateOwner(value.Owner);
        if (value.OperationId == Guid.Empty || value.CorrelationId == Guid.Empty || value.SourceEventId == Guid.Empty
            || value.ExpectedRevision < 0 || value.SourceVersion <= 0 || !Enum.IsDefined(value.Status))
            throw new ArgumentException("A durable mutation requires bounded identities, valid versions and status.",nameof(value));
        var adds = BoundedCopy(value.Adds, MaximumOwnerLeases);
        var releases = BoundedCopy(value.Releases, MaximumOwnerLeases);
        if (value.CompleteSourceSnapshot && releases.Length != 0)
            throw new ArgumentException("A complete committed source snapshot cannot contain lease deltas.",nameof(value));
        if (adds.Any(lease => lease is null) || releases.Any(release => release is null)
            || adds.Select(lease => lease.LeaseId).Distinct().Count() != adds.Length
            || releases.Select(release => release.LeaseId).Distinct().Count() != releases.Length
            || adds.Any(lease => releases.Any(release => release.LeaseId == lease.LeaseId)))
            throw new ArgumentException("A mutation has duplicate/overlapping lease identities.",nameof(value));
        foreach (var lease in adds) ValidateLease(lease, value.Dataset);
        if (releases.Any(release => release.LeaseId == Guid.Empty || release.ExpectedLeaseVersion <= 0))
            throw new ArgumentException("Release requires the exact lease incarnation.",nameof(value));
        if (value.Status != DurableAuthorityStatus.Active && (adds.Length != 0 || releases.Length != 0))
            throw new ArgumentException("Unknown and terminal facts must not contain lease deltas.",nameof(value));
        if (adds.GroupBy(lease => (lease.Purpose, lease.Ticker)).Any(group => group.Count() != 1))
            throw new ArgumentException("A durable owner cannot acquire duplicate effective targets.",nameof(value));
        return value with
        {
            Adds = Array.AsReadOnly(adds.OrderBy(lease => lease.LeaseId).ToArray()),
            Releases = Array.AsReadOnly(releases.OrderBy(release => release.LeaseId).ToArray())
        };
    }

    public static DurableSubscriptionSnapshot Freeze(DurableSubscriptionSnapshot value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateScope(value.Scope, value.Dataset);
        if (value.SchemaVersion != 1 || value.Revision < 0) throw new InvalidDataException("Unsupported durable snapshot.");
        var sources = BoundedCopy(value.Authorities, MaximumAuthorities);
        if (sources.Any(source => source is null)
            || sources.Select(source => source.SourceId).Distinct(StringComparer.Ordinal).Count() != sources.Length
            || sources.Select(source => source.Owner).Distinct().Count() != sources.Length)
            throw new InvalidDataException("Duplicate source/owner in durable snapshot.");
        var allIds = new HashSet<Guid>();
        var total = 0;
        var copy = new List<DurableAuthorityState>(sources.Length);
        foreach (var source in sources)
        {
            Identity(source.SourceId, 256);
            ValidateOwner(source.Owner);
            Identity(source.ReasonCode, 128);
            if (source.SourceVersion <= 0 || source.SourceEventId == Guid.Empty || !Enum.IsDefined(source.Status)
                || source.FactDigest?.Length != 64 || source.FactDigest.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidDataException("Invalid authority watermark in durable snapshot.");
            var leases = BoundedCopy(source.Leases, MaximumOwnerLeases);
            if (leases.Any(lease => lease is null)) throw new InvalidDataException("Null lease in durable snapshot.");
            if (source.Status == DurableAuthorityStatus.Terminal && leases.Length != 0)
                throw new InvalidDataException("Terminal authority cannot retain active leases.");
            if (leases.GroupBy(lease => (lease.Purpose, lease.Ticker)).Any(group => group.Count() != 1))
                throw new InvalidDataException("Duplicate effective owner target.");
            foreach (var lease in leases)
            {
                ValidateLease(lease, value.Dataset);
                if (!allIds.Add(lease.LeaseId)) throw new InvalidDataException("A lease is claimed by multiple owners.");
            }
            total += leases.Length;
            if (total > MaximumLeases) throw new InvalidDataException("Durable lease capacity exceeded.");
            copy.Add(source with { Leases = Array.AsReadOnly(leases.OrderBy(lease => lease.LeaseId).ToArray()) });
        }
        return value with { Authorities = Array.AsReadOnly(copy.OrderBy(source => source.SourceId, StringComparer.Ordinal).ToArray()) };
    }

    public static string RequestDigest(DurableAuthorityMutation frozen) => Digest(frozen);

    /// <summary>Transport operation/correlation/revision do not change the identity of a source fact.</summary>
    public static string FactDigest(DurableAuthorityMutation frozen) 
    {
        ArgumentNullException.ThrowIfNull(frozen);
        return frozen.CompleteSourceSnapshot
        ? Digest(new
        {
            Mode = "CommittedSnapshot/v1",
            frozen.Scope,
            frozen.Dataset,
            frozen.SourceId,
            frozen.SourceVersion,
            frozen.SourceEventId,
            frozen.Owner,
            frozen.Status,
            frozen.ReasonCode,
            frozen.Adds
        }) : Digest(new
        {
            frozen.Scope,
            frozen.Dataset,
            frozen.SourceId,
            frozen.SourceVersion,
            frozen.SourceEventId,
            frozen.Owner,
            frozen.Status,
            frozen.ReasonCode,
            frozen.Adds,
            frozen.Releases
        });
    }

    public static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private static T[] BoundedCopy<T>(IReadOnlyList<T> values, int maximum)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > maximum) throw new ArgumentException("Durable collection exceeds its bound.",nameof(values));
        var copy = values.Take(maximum + 1).ToArray();
        if (copy.Length > maximum) throw new ArgumentException("Durable collection exceeds its bound.",nameof(values));
        return copy;
    }

    private static void ValidateOwner(DurableSubscriptionOwner value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Identity(value.WorkflowType, 64);
        Identity(value.WorkflowId, 128);
        Identity(value.LegId, 128);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Name the public request owning the invalid nested property.")]
    private static void ValidateLease(DurableSubscriptionLease lease, string dataset)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(lease.Ticker, nameof(lease));
        if (lease.LeaseId == Guid.Empty || lease.LeaseVersion <= 0
            || lease.Purpose is not (SubscriptionLeasePurpose.Strategy or SubscriptionLeasePurpose.WorkingOrder or SubscriptionLeasePurpose.Position))
            throw new ArgumentException("Only valid durable ticker leases can be persisted.",nameof(lease));
        var ticker = lease.Ticker;
        if (ticker.PricingPlanId is { } planId && (planId.Length != 64 || !planId.All(Uri.IsHexDigit)))
            throw new ArgumentException("Pricing reconstruction requires an exact plan identity.",nameof(lease));
        _ = new SubscriptionTickerKey(ticker.ProviderScope, ticker.Dataset, ticker.ContractId, ticker.Schema, ticker.AssetKind);
        if (!string.Equals(ticker.Dataset,dataset,StringComparison.Ordinal)) throw new ArgumentException("Ticker is outside the mutation dataset.",nameof(lease));
        if (ticker.AssetKind == SubscriptionAssetKind.FuturesOption)
            Identity(ticker.UnderlyingContractId!, 256);
        else if (ticker.UnderlyingContractId is not null)
            throw new ArgumentException("An outright future has no option dependency.",nameof(lease));
    }

    private static void Identity(string value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || !string.Equals(value,value.Trim(),StringComparison.Ordinal) || value.Any(char.IsControl))
            throw new ArgumentException("Durable identities must be non-empty, canonical and bounded.",nameof(value));
    }
}
