using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertRestorePointParameter(
    DatabaseBackupEventContract Event, bool Eligible, bool LegalHold, bool RestoreTested) : IBindValue
{
    public object Bind() => Values(
        Text(Event.RestorePointId!.Value.Value), Smallint((short)Event.Source.Source), Uuid(Event.Source.BackupSetId?.Value),
        Text(Event.Source.ProtectionSetId.Value), TimestampTz(Event.Source.ObservedUtc.UtcDateTime),
        Smallint((short)Event.VerificationLevel), TimestampTz(Event.VerificationLevel == DatabaseVerificationLevel.None ? null : Event.Source.ObservedUtc.UtcDateTime),
        TimestampTz(RestoreTested ? Event.Source.ObservedUtc.UtcDateTime : null), Boolean(Eligible), Boolean(LegalHold),
        Bigint(Event.ManifestRevision), Text(Event.BackupLineage.SerializeLineage()), Bigint(Event.EventId),
        Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}
