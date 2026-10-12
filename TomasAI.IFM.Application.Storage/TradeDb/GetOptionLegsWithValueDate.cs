using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetOptionLegsWithValueDate(int tradeId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { tradeId };
}
