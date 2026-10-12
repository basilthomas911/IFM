using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertOperationParameter(DatabaseBackupEventContract Event) : IBindValue
{
    public object Bind()
    {
        var e = Event;
        var terminal = e.Source.Phase is DatabaseRecoveryPhase.Completed or DatabaseRecoveryPhase.Failed
            or DatabaseRecoveryPhase.Cancelled or DatabaseRecoveryPhase.Rejected;
        return Values(
            Uuid(e.Source.OperationId.Value), Uuid(e.Source.BackupSetId?.Value), Text(e.Source.ProtectionSetId.Value),
            Smallint((short)e.Source.Source), Smallint((short)e.Source.OperationKind), Smallint((short)e.Source.Phase),
            Smallint((short)e.Outcome), Integer(e.ProgressPercent), Bigint(e.EventId), TimestampTz(e.Source.ObservedUtc.UtcDateTime),
            TimestampTz(terminal ? e.Source.ObservedUtc.UtcDateTime : null), Text(e.SafeDiagnosticReference),
            Text(e.RestorePointId?.Value), Smallint((short)e.RestoreClass), Text(e.FreshTarget?.Profile ?? string.Empty),
            Bigint(e.ValidationRevision), Smallint((short)e.CutoverState), Bigint(e.Source.PolicyRevision),
            Text(e.BackupLineage.SerializeLineage()), Bigint(e.EventId), Uuid(e.Source.SourceEventId));
    }
}
