using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradePlanActivity(
    DateOnly ValueDate,
    DateTime CalculatedAtUtc,
    int PortfolioId,
    int FundId,
    int OrderId,
    int TradeId,
    Guid PositionId,
    sbyte StrategyKind,
    long PlanRevision,
    sbyte State,
    bool RequiresExit,
    string ContentHash,
    byte[] Payload) : IBindValue
{
    public object Bind() =>
        new object?[]
        {
            ValueDate,
            CalculatedAtUtc,
            PortfolioId,
            FundId,
            OrderId,
            TradeId,
            PositionId,
            StrategyKind,
            PlanRevision,
            State,
            RequiresExit,
            ContentHash,
            Payload
        };
}
