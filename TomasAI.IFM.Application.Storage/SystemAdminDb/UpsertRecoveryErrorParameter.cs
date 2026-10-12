using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertRecoveryErrorParameter(DatabaseBackupEventContract Event) : IBindValue
{
    public object Bind() => Values(
        Uuid(Event.Source.OperationId.Value), Uuid(Event.Source.SourceEventId), Smallint((short)Event.ErrorClassification),
        Text(Event.SafeDiagnosticReference), TimestampTz(Event.Source.ObservedUtc.UtcDateTime), Bigint(Event.EventId),
        Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}
