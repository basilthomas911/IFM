using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Accepted desired state is not evidence of a running route, fresh quote or permission to trade.</summary>
public sealed record SubscriptionLeaseResult(
    Guid OperationId, SubscriptionResultCode Code, SubscriptionLeaseView? Lease,
    long DesiredRevision, long RealizedRevision, string? Reason = null)
{
    public IReadOnlyList<SubscriptionLeaseView> SelectedLeases { get; init; } = Array.Empty<SubscriptionLeaseView>();
    public static SubscriptionLeaseResult Disabled(Guid operationId) => new(operationId,
        SubscriptionResultCode.Disabled, null, 0, 0, "Stage 4 lease admission is not enabled.");
}
