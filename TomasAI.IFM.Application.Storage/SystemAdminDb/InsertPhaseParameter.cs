using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertPhaseParameter(DatabaseBackupEventContract Event) : IBindValue
{
    public object Bind() => Values(
        Uuid(Event.Source.OperationId.Value), Smallint((short)Event.Source.Phase), Bigint(Event.EventId),
        Smallint((short)Event.Outcome), Integer(Event.ProgressPercent), TimestampTz(Event.Source.ObservedUtc.UtcDateTime),
        Text(Event.Source.ProducingHostId?.Value), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}
