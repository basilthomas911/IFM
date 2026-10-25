using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseSourceEnvelope : IDatabaseBackupValidatable
{
    [Key(0)] public int ContractVersion { get; init; } = DatabaseRequestEnvelope.CurrentContractVersion;
    [Key(1)] public Guid SourceEventId { get; init; }
    [Key(2)] public DatabaseRecoveryOperationId OperationId { get; init; }
    [Key(3)] public DatabaseBackupSetId? BackupSetId { get; init; }
    [Key(4)] public BackupSource Source { get; init; }
    [Key(5)] public DatabaseProtectionSetId ProtectionSetId { get; init; }
    [Key(6)] public long PolicyRevision { get; init; }
    [Key(7)] public DatabaseRecoveryOperationKind OperationKind { get; init; }
    [Key(8)] public DatabaseRecoveryPhase Phase { get; init; }
    [Key(9)] public DatabaseBackupHostId? ProducingHostId { get; init; }
    [Key(10)] public long SourceRevisionOrSequence { get; init; }
    [Key(11)] public Guid CorrelationId { get; init; }
    [Key(12)] public Guid CausationId { get; init; }
    [Key(13)] public DateTimeOffset ObservedUtc { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "This validates persisted instance properties, not method arguments; the property name identifies the invalid stored field.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This validates persisted instance properties, not method arguments; the property name identifies the invalid stored field.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This validates persisted instance properties, not method arguments; the property name identifies the invalid stored field.")]
    public void Validate()
    {
        if (ContractVersion != DatabaseRequestEnvelope.CurrentContractVersion) throw new ArgumentOutOfRangeException(nameof(ContractVersion));
        if (SourceEventId == Guid.Empty || OperationId.Value == Guid.Empty) throw new ArgumentException("Source event and operation IDs are required.");
        DatabaseBackupEnumValidation.RequireConcrete(Source);
        DatabaseBackupEnumValidation.RequireDefined(OperationKind, nameof(OperationKind));
        DatabaseBackupEnumValidation.RequireDefined(Phase, nameof(Phase));
        if (string.IsNullOrWhiteSpace(ProtectionSetId.Value)) throw new ArgumentException("Protection set is required.");
        if (PolicyRevision < 0 || SourceRevisionOrSequence < 0) throw new ArgumentOutOfRangeException(nameof(SourceRevisionOrSequence));
        DatabaseRequestEnvelope.RequireUtc(ObservedUtc, nameof(ObservedUtc));
    }
}
