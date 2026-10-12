using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

/// <summary>Immutable audit evidence linking the signed plan, owner approval, deletion result and retained recovery point.</summary>
public sealed record AwsRetentionExecutionEvidence(
    DatabaseRecoveryOperationId OperationId,
    AwsRetentionPlanDocument Plan,
    AwsSignatureEnvelope PlanSignature,
    AwsRetentionExecutionApproval Approval,
    AwsRetentionDeletionResult Result,
    DatabaseRestorePointId PreservedRestorePointId,
    bool PreservedBackupVerified,
    DateTimeOffset CompletedUtc);
public sealed record AwsRetentionFailureEvidence(
    DatabaseRetentionPlanId PlanId,
    long Revision,
    DatabaseArtifactReplicaId ReplicaId,
    AwsRetentionPlanObject[] DeletedVersions,
    string FailureType,
    DateTimeOffset ObservedUtc);