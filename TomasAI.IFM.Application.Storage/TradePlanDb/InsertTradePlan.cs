using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradePlan(
    int PortfolioId,
    int FundId,
    int OrderId,
    int TradeId,
    Guid PositionId,
    DateOnly ValueDate,
    long PlanRevision,
    DateTime CalculatedAtUtc,
    string State,
    bool RequiresExit,
    string ContentHash,
    byte[] Payload) : IBindValue
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
            PlanRevision,
            CalculatedAtUtc,
            State,
            RequiresExit,
            ContentHash,
            Payload
        };
}
