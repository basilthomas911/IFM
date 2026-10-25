using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

/// <summary>Identifies a manual Portfolio Fund order transition at an expected version.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ManualFundOrderMutationRequest
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }
    [Key(3)] public long ExpectedOrderVersion { get; init; }
    [Key(4)] public string Reason { get; init; } = string.Empty;
    [Key(5)] public DateTime RequestedAtUtc { get; init; }
}
