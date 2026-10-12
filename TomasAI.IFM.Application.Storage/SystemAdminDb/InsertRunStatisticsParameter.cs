using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertRunStatisticsParameter(DatabaseBackupEventContract Event) : IBindValue
{
    public object Bind()
    {
        var stats = Event.Statistics!;
        return Values(
            Uuid(Event.Source.OperationId.Value), Smallint((short)Event.Source.Source), Smallint((short)stats.Phase),
            Smallint((short)stats.Engine), Bigint(Event.EventId), TimestampTz(stats.StartedUtc?.UtcDateTime),
            TimestampTz(stats.CompletedUtc?.UtcDateTime), Bigint(stats.Elapsed?.Ticks), Bigint(stats.SourceBytes),
            Bigint(stats.StoredBytes), Bigint(stats.TransferredBytes), Bigint(stats.RestoredBytes), Integer(stats.ArtifactCount),
            Double(stats.AverageThroughputBytesPerSecond), Double(stats.PeakThroughputBytesPerSecond), Integer(stats.RetryCount),
            Integer(stats.WarningCount), Bigint(stats.AchievedRpo?.Ticks), Bigint(stats.AchievedRto?.Ticks),
            Text(Event.Source.ProducingHostId?.Value), Bigint(Event.Source.PolicyRevision), Bigint(Event.EventId),
            Uuid(Event.Source.SourceEventId));
    }
}
