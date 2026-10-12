using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTickTradeData(object?[] Values) : IBindValue
{
    public object Bind() => Values;
}

