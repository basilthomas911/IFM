using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct PortfolioExecutionOrderId(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] int OrderId)
{
    [IgnoreMember] public bool IsValid => PortfolioId > 0 && FundId > 0 && OrderId > 0;
}
