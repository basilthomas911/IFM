using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetIntrinsicTimeStrategyWorkflowsByEntity(
    string WorkflowEntityId,
    DateTime BeforeUtc,
    int PageSize) : IBindValue
{
    public object Bind() => new object?[] { WorkflowEntityId, BeforeUtc, PageSize };
}
