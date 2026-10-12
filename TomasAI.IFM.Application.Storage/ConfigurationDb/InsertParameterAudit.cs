using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertParameterAudit(Guid OperationId, Guid EntityId, long Revision, string Body) : IBindValue
{
    public object Bind() => Values(Uuid(OperationId), Uuid(EntityId), Bigint(Revision), Text(Body));
}
