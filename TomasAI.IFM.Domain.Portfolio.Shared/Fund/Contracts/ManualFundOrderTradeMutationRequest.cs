using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

/// <summary>Identifies a manual Portfolio Fund trade mutation at an expected order version.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ManualFundOrderTradeMutationRequest
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }
    [Key(3)] public long ExpectedOrderVersion { get; init; }
    [Key(4)] public int TradeId { get; init; }
    [Key(5)] public string TradeState { get; init; } = string.Empty;
    [Key(6)] public DateTime RequestedAtUtc { get; init; }
    /// <summary>Optionally binds the selected setup to its committed execution order.</summary>
    [Key(7)] public int? ExecutionOrderId { get; init; }
    /// <summary>The corresponding reserved established trade identifier.</summary>
    [Key(8)] public int? ExecutionTradeId { get; init; }
}
