using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

/// <summary>Supplies producer-allocated identities and the projected parent evidence for a Fund order.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FundOrderPreparation
{
    [Key(0)] public int OrderId { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the typed numeric wire payload for prepared trade identities.")]
    [Key(1)] public int[] TradeIds { get; init; } = [];
    [Key(2)] public PortfolioReadModel? Parent { get; init; }
    [Key(3)] public long? ParentSourceRevision { get; init; }

    /// <summary>Binds an allocation receipt to one Fund command and identifier position.</summary>
    public static Guid AllocationOperationId(PortfolioFundId fund, Guid commandId, PortfolioBusinessIdentityKind kind, int index)
    {
        var scope = FormattableString.Invariant($"PortfolioFund.OrderIdentity.v1:{fund.Format()}:{commandId:N}:{(int)kind}:{index}");
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(scope)).AsSpan(0, 16));
    }
}
