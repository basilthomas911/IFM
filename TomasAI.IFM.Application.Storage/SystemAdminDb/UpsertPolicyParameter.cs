using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertPolicyParameter(DatabaseBackupEventContract Event, string DefinitionJson, bool Enforced) : IBindValue
{
    public object Bind() => Values(
        Text(Event.Request?.EnvironmentIdentity ?? string.Empty), Text(Event.PolicyId!.Value.Value),
        Bigint(Event.Source.PolicyRevision), Text(DefinitionJson), Boolean(Enforced), Bigint(Event.EventId),
        Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}
