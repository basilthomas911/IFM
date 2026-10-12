using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertProjectionReceiptParameter(
    string ProjectorName, long EventId, string EventHash, Guid SourceEventId, DateTime AppliedUtc) : IBindValue
{
    public object Bind() => Values(Text(ProjectorName), Bigint(EventId), Text(EventHash), Uuid(SourceEventId), TimestampTz(AppliedUtc));
}
