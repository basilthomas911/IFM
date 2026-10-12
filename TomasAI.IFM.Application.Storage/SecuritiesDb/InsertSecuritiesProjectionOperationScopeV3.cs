using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertSecuritiesProjectionOperationScopeV3(string projectionName, Guid operationId, string scopeType, string scopeKey) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId, scopeType, scopeKey };
}
