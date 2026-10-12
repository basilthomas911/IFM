using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetExactExitPositionWorkflow(
    int PortfolioId,
    int FundId,
    int OrderId,
    int TradeId,
    Guid PositionId,
    DateOnly ValueDate,
    DateTime UpdatedAtUtc,
    Guid ExitDecisionId,
    long StageRevision) : IBindValue
{
    public object Bind() =>
        new object?[]
        {
            PortfolioId,
            FundId,
            OrderId,
            TradeId,
            PositionId,
            ValueDate,
            UpdatedAtUtc,
            ExitDecisionId,
            StageRevision
        };
}
