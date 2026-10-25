using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

[MessagePackObject(AllowPrivate = true)]
public sealed record CreateManualFundOrderRequest
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public long PortfolioVersion { get; init; }
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public long FundMandateVersion { get; init; }
    [Key(4)] public string Reference { get; init; } = string.Empty;
    [Key(5)] public Guid IdempotencyKey { get; init; }
    [Key(6)] public DateTime RequestedAtUtc { get; init; }
    [Key(7)] public DateTime ExpiresAtUtc { get; init; }
}
