using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetIntrinsicTimeStrategyWorkflowTimeline(
    Guid WorkflowId,
    long AfterEventId,
    int PageSize) : IBindValue
{
    public object Bind() => new object?[] { WorkflowId, AfterEventId, PageSize };
}
