using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

public interface IDatabaseBackupValidatable
{
    void Validate();
}

[MessagePackObject]
public sealed record DatabaseRequestEnvelope : IDatabaseBackupValidatable
{
    public const int CurrentContractVersion = 1;

    [Key(0)] public int ContractVersion { get; init; } = CurrentContractVersion;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public string CallerIdentity { get; init; } = string.Empty;
    [Key(3)] public string AuthorizationReference { get; init; } = string.Empty;
    [Key(4)] public string[] CallerRoles { get; init; } = [];
    [Key(5)] public DatabaseRequestOrigin Origin { get; init; }
    [Key(6)] public Guid CorrelationId { get; init; }
    [Key(7)] public Guid CausationId { get; init; }
    [Key(8)] public string EnvironmentIdentity { get; init; } = string.Empty;
    [Key(9)] public DateTimeOffset CreatedUtc { get; init; }

    public void Validate()
    {
        if (ContractVersion != CurrentContractVersion) throw new ArgumentOutOfRangeException(nameof(ContractVersion));
        if (RequestId == Guid.Empty) throw new ArgumentException("Request ID is required.", nameof(RequestId));
        DatabaseBackupEnumValidation.RequireDefined(Origin, nameof(Origin));
        ValidateSafeText(CallerIdentity, nameof(CallerIdentity));
        ValidateSafeText(AuthorizationReference, nameof(AuthorizationReference));
        ValidateSafeText(EnvironmentIdentity, nameof(EnvironmentIdentity));
        if (CallerRoles.Length > DatabaseBackupContractLimits.MaximumCollectionCount) throw new ArgumentOutOfRangeException(nameof(CallerRoles));
        foreach (var role in CallerRoles) ValidateSafeText(role, nameof(CallerRoles));
        RequireUtc(CreatedUtc, nameof(CreatedUtc));
    }

    internal static void ValidateSafeText(string? value, string parameterName, int maximumLength = DatabaseBackupContractLimits.SafeTextLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maximumLength || value.Any(char.IsControl)) throw new ArgumentOutOfRangeException(parameterName);
    }

    internal static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value == default || value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be a non-default UTC DateTimeOffset.", parameterName);
    }
}

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
