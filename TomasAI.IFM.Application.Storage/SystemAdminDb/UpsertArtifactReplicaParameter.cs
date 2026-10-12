using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertArtifactReplicaParameter(DatabaseBackupEventContract Event) : IBindValue
{
    public object Bind()
    {
        var replica = Event.ArtifactReplica!;
        return Values(
            Text(replica.ReplicaId.Value), Smallint((short)Event.Source.Source), Uuid(Event.Source.OperationId.Value),
            Text(replica.ArtifactId.Value), Smallint((short)replica.Engine), Smallint((short)replica.State),
            Text(replica.SafeDestinationReference), Bigint(replica.Bytes), Bigint(Event.EventId), Bigint(Event.EventId),
            Uuid(Event.Source.SourceEventId));
    }
}
