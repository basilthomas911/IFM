using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertRetentionParameter(DatabaseBackupEventContract Event, string RetainJson, string DeleteJson, bool Approved) : IBindValue
{
    public object Bind() => Values(
        Uuid(Event.RetentionPlanId!.Value.Value), Smallint((short)Event.Source.Source), Bigint(Event.RetentionPlanRevision),
        TimestampTz((Event.EvaluationBoundaryUtc == default ? Event.Source.ObservedUtc : Event.EvaluationBoundaryUtc).UtcDateTime),
        Text(RetainJson), Text(DeleteJson), Boolean(Approved), Smallint((short)Event.Outcome), Bigint(Event.EventId),
        Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}
