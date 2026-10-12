using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMarketConditionHistory(
    int FundId, string InstrumentRoot, string TargetHorizon, DateTime BeforeUtc, int PageSize) : IBindValue
{
    public object Bind() => new object?[] { FundId, InstrumentRoot, TargetHorizon, BeforeUtc, PageSize };
}
