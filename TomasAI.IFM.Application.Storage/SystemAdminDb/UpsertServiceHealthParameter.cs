using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertServiceHealthParameter(DatabaseBackupEventContract Event, bool Reconciled) : IBindValue
{
    public object Bind() => Values(
        Text(Event.Request?.EnvironmentIdentity ?? "system"), Smallint((short)Event.Source.Source),
        Text(Event.Source.ProducingHostId!.Value.Value), Smallint((short)Event.CapabilityState),
        Boolean(Event.CapabilityState == DatabaseServiceCapabilityState.Ready), Bigint(Event.Source.SourceRevisionOrSequence),
        TimestampTz(Event.Source.ObservedUtc.UtcDateTime), Text(Event.SafeDiagnosticReference), Boolean(Reconciled),
        Bigint(Event.EventId), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}
