using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertParameterOperation(Guid OperationId, Guid SetId, long Revision, string RequestHash, int Version, DateTime CommittedAtUtc) : IBindValue
{
    public object Bind() => Values(Uuid(OperationId), Uuid(SetId), Bigint(Revision), Text(RequestHash), Integer(Version), TimestampTz(CommittedAtUtc));
}
