using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct PortfolioExitWorkflowId(
    [property: Key(0)] PortfolioPositionReference Position,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] Guid ExitDecisionId)
{
    [IgnoreMember] public bool IsValid => Position.IsValid && ValueDate != default && ExitDecisionId != Guid.Empty;
}
