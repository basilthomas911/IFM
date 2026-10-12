using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

/// <summary>
/// One source stream controls exactly one owner in this initial store subset. Active changes are
/// explicit adds/releases, never a projection replacement. Unknown retains all known leases.
/// A terminal fact explicitly releases that owner's leases only. Source authentication belongs
/// to an approved application adapter; this internal persistence contract provides no authorization.
/// </summary>
public sealed record DurableAuthorityMutation(
    string Scope, string Dataset, Guid OperationId, Guid CorrelationId, long ExpectedRevision,
    string SourceId, long SourceVersion, Guid SourceEventId, DurableSubscriptionOwner Owner,
    DurableAuthorityStatus Status, string ReasonCode,
    IReadOnlyList<DurableSubscriptionLease> Adds, IReadOnlyList<DurableSubscriptionRelease> Releases,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool CompleteSourceSnapshot = false);
