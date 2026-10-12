using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteActiveIntrinsicTimeStrategyWorkflow(string WorkflowEntityId) : IBindValue
{
    public object Bind() => new object?[] { WorkflowEntityId };
}
