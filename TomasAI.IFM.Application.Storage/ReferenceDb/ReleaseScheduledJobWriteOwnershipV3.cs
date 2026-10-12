using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ReleaseScheduledJobWriteOwnershipV3(
    string scopeType,
    string scopeKey,
    Guid operationId) : IBindValue
{
    public object Bind() => new object?[] { scopeType, scopeKey, operationId };
}
