using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ValueDateParameter(DateOnly ValueDate) : IBindValue
{
    public object Bind() => new object?[] { ValueDate };
}
