using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Processing;

/// <summary>Creates repeatable journal intents for an explicitly owner-authorized Development retention schedule.</summary>
public static class AwsAutomaticRetentionSchedule
{
    public static DatabaseExecutionIntent[] Create(AwsCloudDatabaseBackupOptions options, DateTimeOffset now)
    {
        if (!options.Enabled || !options.AcceptBackupRequests || !options.AutomaticRetentionEnabled) return [];
        options.Validate();
        var slotTicks = now.UtcTicks / options.AutomaticRetentionInterval.Ticks * options.AutomaticRetentionInterval.Ticks;
        var slot = new DateTimeOffset(slotTicks, TimeSpan.Zero);
        return options.PostgreSqlProtectionSets.Concat(options.ScyllaProtectionSets)
            .SelectMany(set => new[] { "aws-primary", "aws-recovery" }.Select(replica =>
            {
                var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
                    $"aws-retention-command-v1:{options.Environment}:{set}:{replica}:{options.DefaultRetentionDays}:{options.AutomaticRetentionApprovalReference}:{slot:O}")).AsSpan(0, 16));
                var operation = new DatabaseRecoveryOperationId(id);
                var source = new DatabaseSourceEnvelope
                {
                    SourceEventId = id, OperationId = operation, Source = BackupSource.AwsCloud,
                    ProtectionSetId = new DatabaseProtectionSetId(set), PolicyRevision = 1,
                    OperationKind = DatabaseRecoveryOperationKind.Retention, Phase = DatabaseRecoveryPhase.Authorized,
                    SourceRevisionOrSequence = 1, CorrelationId = id, CausationId = id, ObservedUtc = slot
                };
                var request = new DatabaseRequestEnvelope
                {
                    RequestId = id, CallerIdentity = "DatabaseBackupHost.AutomaticRetention",
                    AuthorizationReference = options.AutomaticRetentionApprovalReference,
                    CallerRoles = ["DevelopmentOwnerRetentionSchedule"],
                    Origin = DatabaseRequestOrigin.ScheduledTask, CorrelationId = id, CausationId = id,
                    EnvironmentIdentity = options.Environment.ToString(), CreatedUtc = slot
                };
                return new DatabaseExecutionIntent { ExecutionEvent = new DatabaseRetentionEvaluationRequestedEvent
                {
                    Subject = new ActorSubject(ActorType.Event, "DatabaseBackupEvent", "RetentionEvaluationRequested", operation.Format()),
                    Id = id, EntityId = operation, CommandId = id, AggregateId = operation.Format(),
                    EventSource = "DatabaseBackupHost", ReceivedOn = slot.UtcDateTime,
                    Source = source, Request = request, RetentionPlanId = new DatabaseRetentionPlanId(id),
                    RetentionPlanRevision = 1, EvaluationBoundaryUtc = slot.AddDays(-options.DefaultRetentionDays),
                    RequiredDestinations = [new DatabaseLogicalDestination(replica, true)]
                } };
            })).ToArray();
    }
}