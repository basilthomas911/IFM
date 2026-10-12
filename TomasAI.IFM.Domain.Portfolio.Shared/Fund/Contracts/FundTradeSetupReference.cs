using MessagePack;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

/// <summary>Stable setup identity carried by backend execution; null for orders without a manual setup.</summary>
[MessagePackObject]
public sealed record FundTradeSetupReference
{
    [Key(0)] public int OrderId { get; init; }
    [Key(1)] public int TradeId { get; init; }
    [Key(2)] public int FundId { get; init; }
}
