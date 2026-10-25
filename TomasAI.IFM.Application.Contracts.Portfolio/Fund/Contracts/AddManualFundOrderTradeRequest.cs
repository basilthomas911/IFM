using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

/// <summary>Describes one operator-authored trade to add to a manual Portfolio Fund order.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record AddManualFundOrderTradeRequest
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }
    [Key(3)] public long ExpectedOrderVersion { get; init; }
    [Key(4)] public int TradeId { get; init; }
    [Key(5)] public string TradeType { get; init; } = string.Empty;
    [Key(6)] public DateOnly? TradeDate { get; init; }
    [Key(7)] public DateOnly? MaturityDate { get; init; }
    [Key(8)] public string TradeState { get; init; } = string.Empty;
    [Key(9)] public string TradeAction { get; init; } = string.Empty;
    [Key(10)] public string Reference { get; init; } = string.Empty;
    [Key(11)] public bool PrimaryTrade { get; init; }
    [Key(12)] public string BaseContractSymbol { get; init; } = string.Empty;
    [Key(13)] public DateTime RequestedAtUtc { get; init; }
    /// <summary>The canonical, provider-independent identifier of the selected futures contract.</summary>
    [Key(14)] public string BaseContractId { get; init; } = string.Empty;
    /// <summary>The intended effective value date; actual execution dates remain unset during manual entry.</summary>
    [Key(15)] public DateOnly EffectiveDate { get; init; }
    /// <summary>Reads the effective date of older commands without treating it as an actual fill date.</summary>
    [IgnoreMember] public DateOnly SetupEffectiveDate => EffectiveDate != default ? EffectiveDate : TradeDate ?? default;
}
