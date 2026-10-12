using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetIntrinsicTimeStrategyWorkflowsByStatusDay(
    string Status,
    DateOnly StartedDate,
    int PageSize) : IBindValue
{
    public object Bind() => new object?[] { Status, StartedDate, PageSize };
}
