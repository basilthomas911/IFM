using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct OperationListParameter(
    BackupSource Source, string? ProtectionSetId, DateTime? FromUtc, DateTime? ToUtc,
    Guid? Continuation, int PageSize) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source), Text(ProtectionSetId), TimestampTz(FromUtc), TimestampTz(ToUtc), Uuid(Continuation), Integer(PageSize));
}
