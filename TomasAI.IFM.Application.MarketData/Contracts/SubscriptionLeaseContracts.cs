using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public enum SubscriptionAssetKind { Futures, FuturesOption }
public enum SubscriptionLeasePurpose { Discovery, Composer, Strategy, WorkingOrder, Position }
public enum StrategyMarketDataProfile { EsMonthlyIronCondor, EsWeeklyVerticalSpread, EsDailyFutures }
public enum SubscriptionResultCode
{
    Disabled, DesiredAccepted, Active, AlreadyOwned, Released, NotOwned, Recovering, Closed,
    Expired, Conflict, InvalidContract, CapacityExceeded, PricingUnavailable, StaleData,
    PersistenceUnavailable, OwnershipUnverified, Timeout, Cancelled, UnsupportedProtocol
}

/// <summary>Validated server-authorized scope plus the existing workflow identity; not an authorization token.</summary>
public sealed record SubscriptionOwnerKey
{
    public string Scope { get; }
    public TickerStreamOwner Owner { get; }

    public SubscriptionOwnerKey(string scope, TickerStreamOwner owner)
    {
        SubscriptionIdentity.Validate(scope, 128);
        owner.Validate();
        SubscriptionIdentity.Validate(owner.WorkflowType, 64);
        SubscriptionIdentity.Validate(owner.WorkflowId, 128);
        SubscriptionIdentity.Validate(owner.LegId, 128);
        Scope = scope;
        Owner = owner;
    }
}

/// <summary>Canonical domain identity. This contains no native instrument ID or provider credential.</summary>
public sealed record SubscriptionTickerKey
{
    public string ProviderScope { get; }
    public string Dataset { get; }
    public string ContractId { get; }
    public string Schema { get; }
    public SubscriptionAssetKind AssetKind { get; }

    public SubscriptionTickerKey(string providerScope, string dataset, string contractId,
        string schema, SubscriptionAssetKind assetKind)
    {
        SubscriptionIdentity.Validate(providerScope, 64);
        SubscriptionIdentity.Validate(dataset, 64);
        SubscriptionIdentity.Validate(contractId, 256);
        SubscriptionIdentity.Validate(schema, 32);
        if (!Enum.IsDefined(assetKind)) throw new ArgumentOutOfRangeException(nameof(assetKind));
        ProviderScope = providerScope;
        Dataset = dataset;
        ContractId = contractId;
        Schema = schema;
        AssetKind = assetKind;
    }
}

/// <summary>Immutable exact discovery universe. Provider mapping/expiry validation is a separate admission step.</summary>
public sealed class SubscriptionChainKey : IEquatable<SubscriptionChainKey>
{
    public const int MaximumContracts = 512;
    public SubscriptionTickerKey Underlying { get; }
    public DateOnly MaturityDate { get; }
    public DateOnly ValueDate { get; }
    public IReadOnlyList<SubscriptionTickerKey> Options { get; }
    public string ContractSetDigest { get; }

    public SubscriptionChainKey(SubscriptionTickerKey underlying, DateOnly maturityDate,
        DateOnly valueDate, IEnumerable<SubscriptionTickerKey> options)
    {
        ArgumentNullException.ThrowIfNull(underlying);
        ArgumentNullException.ThrowIfNull(options);
        if (underlying.AssetKind != SubscriptionAssetKind.Futures || valueDate == default
            || maturityDate < valueDate) throw new ArgumentException("Chain dates/underlying are invalid.");
        var copy = options.Take(MaximumContracts + 1).ToArray();
        if (copy.Length is < 1 or > MaximumContracts || copy.Any(item => item is null
                || item.AssetKind != SubscriptionAssetKind.FuturesOption
                || item.ProviderScope != underlying.ProviderScope || item.Dataset != underlying.Dataset
                || item.Schema != underlying.Schema)
            || copy.Distinct().Count() != copy.Length)
            throw new ArgumentException("A chain requires bounded unique options in the underlying routing scope.");
        Underlying = underlying;
        MaturityDate = maturityDate;
        ValueDate = valueDate;
        Options = Array.AsReadOnly(copy.OrderBy(item => item.ContractId, StringComparer.Ordinal).ToArray());
        ContractSetDigest = SubscriptionIdentity.Digest(writer =>
        {
            SubscriptionIdentity.Write(writer, underlying);
            writer.Write(maturityDate.DayNumber);
            writer.Write(valueDate.DayNumber);
            writer.Write(Options.Count);
            foreach (var option in Options) SubscriptionIdentity.Write(writer, option);
        });
    }

    public bool Equals(SubscriptionChainKey? other) => ReferenceEquals(this, other) || other is not null
        && Underlying == other.Underlying && MaturityDate == other.MaturityDate
        && ValueDate == other.ValueDate && ContractSetDigest == other.ContractSetDigest && Options.SequenceEqual(other.Options);
    public override bool Equals(object? obj) => obj is SubscriptionChainKey other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(ContractSetDigest);
}

/// <summary>One ticker or one exact chain; callers cannot supply both or neither.</summary>
public sealed record SubscriptionTarget
{
    public SubscriptionTickerKey? Ticker { get; }
    public SubscriptionTickerKey? Underlying { get; }
    public SubscriptionChainKey? Chain { get; }
    public string Dataset => Ticker?.Dataset ?? Chain!.Underlying.Dataset;
    public SubscriptionTarget(SubscriptionTickerKey ticker, SubscriptionTickerKey? underlying = null)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        if (ticker.AssetKind == SubscriptionAssetKind.FuturesOption
            ? underlying is null || underlying.AssetKind != SubscriptionAssetKind.Futures
                || underlying.ProviderScope != ticker.ProviderScope || underlying.Dataset != ticker.Dataset
                || underlying.Schema != ticker.Schema
            : underlying is not null)
            throw new ArgumentException("An option requires its resolved futures dependency; an outright has none.");
        Ticker = ticker;
        Underlying = underlying;
    }
    public SubscriptionTarget(SubscriptionChainKey chain) => Chain = chain ?? throw new ArgumentNullException(nameof(chain));
}

/// <summary>Host epoch and lease incarnation fence stale clients. Renewal increments Version.</summary>
public readonly record struct SubscriptionLeaseToken(Guid HostEpochId, Guid LeaseId, long Version);

public sealed record SubscriptionAcquireRequest(
    Guid OperationId, Guid HostEpochId, Guid CorrelationId, SubscriptionOwnerKey Owner, SubscriptionTarget Target,
    SubscriptionLeasePurpose Purpose, DateTimeOffset DeadlineUtc);
public sealed record SubscriptionRenewRequest(
    Guid OperationId, Guid CorrelationId, SubscriptionOwnerKey Owner, SubscriptionLeaseToken Lease,
    DateTimeOffset DeadlineUtc);
public sealed record SubscriptionReleaseRequest(
    Guid OperationId, Guid CorrelationId, SubscriptionOwnerKey Owner, SubscriptionLeaseToken Lease,
    DateTimeOffset DeadlineUtc);

public sealed record SubscriptionLeaseSelection(SubscriptionOwnerKey Owner, SubscriptionTarget Target);

/// <summary>Immutable atomic desired-intent batch; does not perform discovery-to-ready handoff or leg selection.</summary>
public sealed class SubscriptionAcquireBatchRequest : IEquatable<SubscriptionAcquireBatchRequest>
{
    public const int MaximumSelections = 128;
    public Guid OperationId { get; }
    public Guid HostEpochId { get; }
    public Guid CorrelationId { get; }
    public SubscriptionOwnerKey Owner { get; }
    public IReadOnlyList<SubscriptionLeaseSelection> Selections { get; }
    public SubscriptionLeasePurpose Purpose { get; }
    public DateTimeOffset DeadlineUtc { get; }

    public SubscriptionAcquireBatchRequest(Guid operationId, Guid hostEpochId, Guid correlationId,
        SubscriptionOwnerKey owner, IEnumerable<SubscriptionLeaseSelection> selections,
        SubscriptionLeasePurpose purpose, DateTimeOffset deadlineUtc)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(selections);
        var copy = selections.Take(MaximumSelections + 1).ToArray();
        if (copy.Length is < 1 or > MaximumSelections || copy.Any(item => item?.Owner is null || item.Target is null)
            || copy.Distinct().Count() != copy.Length)
            throw new ArgumentException("A batch requires a bounded distinct complete selection.");
        OperationId = operationId;
        HostEpochId = hostEpochId;
        CorrelationId = correlationId;
        Owner = owner;
        Selections = Array.AsReadOnly(copy);
        Purpose = purpose;
        DeadlineUtc = deadlineUtc;
    }

    public bool Equals(SubscriptionAcquireBatchRequest? other) => ReferenceEquals(this, other) || other is not null
        && OperationId == other.OperationId && HostEpochId == other.HostEpochId
        && CorrelationId == other.CorrelationId && Owner == other.Owner && Purpose == other.Purpose
        && DeadlineUtc == other.DeadlineUtc && Selections.SequenceEqual(other.Selections);
    public override bool Equals(object? obj) => obj is SubscriptionAcquireBatchRequest other && Equals(other);
    public override int GetHashCode() => OperationId.GetHashCode();
}

public sealed record SubscriptionOwnerQuery(SubscriptionOwnerKey Owner, int Offset = 0, int PageSize = 100);

public sealed record SubscriptionLeaseView(
    SubscriptionLeaseToken Token, SubscriptionOwnerKey Owner, SubscriptionTarget Target,
    SubscriptionLeasePurpose Purpose, DateTimeOffset? ExpiresAtUtc)
{
    public bool IsDurable => Purpose is SubscriptionLeasePurpose.Strategy
        or SubscriptionLeasePurpose.WorkingOrder or SubscriptionLeasePurpose.Position;
}

/// <summary>Accepted desired state is not evidence of a running route, fresh quote or permission to trade.</summary>
public sealed record SubscriptionLeaseResult(
    Guid OperationId, SubscriptionResultCode Code, SubscriptionLeaseView? Lease,
    long DesiredRevision, long RealizedRevision, string? Reason = null)
{
    public IReadOnlyList<SubscriptionLeaseView> SelectedLeases { get; init; } = Array.Empty<SubscriptionLeaseView>();
    public static SubscriptionLeaseResult Disabled(Guid operationId) => new(operationId,
        SubscriptionResultCode.Disabled, null, 0, 0, "Stage 4 lease admission is not enabled.");
}

public static class SubscriptionIdentity
{
    internal static void Validate(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength
            || value != value.Trim() || value.Any(char.IsControl))
            throw new ArgumentException("Subscription identities must be bounded, non-empty and canonical.");
    }

    internal static void Write(BinaryWriter writer, SubscriptionTickerKey key)
    {
        writer.Write(key.ProviderScope);
        writer.Write(key.Dataset);
        writer.Write(key.ContractId);
        writer.Write(key.Schema);
        writer.Write((int)key.AssetKind);
    }

    internal static string Digest(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(1);
            write(writer);
        }
        return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length))));
    }
}
