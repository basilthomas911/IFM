using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InvalidateSecuritiesProjectionStateV3(Guid generation, string projectionName) : IBindValue
{
    public object Bind() => new object?[] { generation, projectionName };
}
