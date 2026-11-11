using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

/// <summary>Prepares a durable intent command before persistence acquires the dataset lock.</summary>
public sealed class DurableSubscriptionCommand
{
    DurableSubscriptionCommand(DurableAuthorityMutation input, string requestDigest, string factDigest)
    {
        Input = input;
        RequestDigest = requestDigest;
        FactDigest = factDigest;
    }

    public DurableAuthorityMutation Input { get; }
    public string RequestDigest { get; }
    public string FactDigest { get; }

    /// <summary>Validates and freezes the submitted authority mutation and derives its stable digests.</summary>
    public static DurableSubscriptionCommand Prepare(DurableAuthorityMutation mutation)
    {
        var input = DurableSubscriptionContract.Freeze(mutation);
        return new(input, DurableSubscriptionContract.RequestDigest(input), DurableSubscriptionContract.FactDigest(input));
    }

    /// <summary>Computes the next revision from the locked snapshot without I/O or callbacks.</summary>
    public DurableSubscriptionCommitPlan Plan(DurableSubscriptionSnapshot current)
    {
        var code = DurableSubscriptionTransition.Apply(current, Input, FactDigest, out var next);
        if (code != DurableIntentResultCode.Committed)
            return new(code, next.Revision, null, [], []);

        var currentIds = current.Authorities.SelectMany(authority => authority.Leases).Select(lease => lease.LeaseId).ToHashSet();
        var additions = Input.Adds.Where(lease => !currentIds.Contains(lease.LeaseId)).ToArray();
        var remainingIds = next.Authorities.SelectMany(authority => authority.Leases).Select(lease => lease.LeaseId).ToHashSet();
        var removed = currentIds.Where(id => !remainingIds.Contains(id)).ToArray();
        return new(code, next.Revision, JsonSerializer.Serialize(next), additions, removed);
    }
}

/// <summary>Contains the proposed revision and row changes before the immutable lease fence is checked.</summary>
public sealed record DurableSubscriptionCommitPlan(DurableIntentResultCode Code, long Revision,
    string? SnapshotJson, IReadOnlyList<DurableSubscriptionLease> Additions, IReadOnlyList<Guid> Removed)
{
    /// <summary>Checks the payload bound after the identity fence, preserving rejection precedence.</summary>
    public bool ExceedsSnapshotCapacity => SnapshotJson is not null &&
        Encoding.UTF8.GetByteCount(SnapshotJson) > DurableSubscriptionContract.MaximumSnapshotBytes;
}
