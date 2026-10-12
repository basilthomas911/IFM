using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertAssignmentRevision(Guid AssignmentId, long Revision, Guid OperationId, string RequestHash, string Body) : IBindValue
{
    public object Bind() => Values(Uuid(AssignmentId), Bigint(Revision), Uuid(OperationId), Text(RequestHash), Text(Body));
}
