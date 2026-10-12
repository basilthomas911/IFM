using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradePlanForwardLossRatio(int partitionId, DateOnly valueDate, double forwardLossRatio, long sequenceId) : IBindValue
{
    public object Bind() => new object?[] { partitionId, valueDate, forwardLossRatio, sequenceId };
}
