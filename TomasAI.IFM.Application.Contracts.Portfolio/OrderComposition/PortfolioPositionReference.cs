using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct PortfolioPositionReference(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] int OrderId,
    [property: Key(3)] int TradeId,
    [property: Key(4)] Guid PositionId)
{
    [IgnoreMember] public bool IsValid => PortfolioId > 0 && FundId > 0 && OrderId > 0 && TradeId > 0 && PositionId != Guid.Empty;
    public string Format() => $"{PortfolioId}.{FundId}.{OrderId}.{TradeId}.{PositionId:N}";
}
