using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

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
            throw new ArgumentException("A batch requires a bounded distinct complete selection.",nameof(selections));
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
