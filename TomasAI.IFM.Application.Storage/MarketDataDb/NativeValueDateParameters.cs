using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeValueDateParameters(DateOnly ValueDate) : IBindValue
{
    public object Bind() => Values(Date(ValueDate));
}
