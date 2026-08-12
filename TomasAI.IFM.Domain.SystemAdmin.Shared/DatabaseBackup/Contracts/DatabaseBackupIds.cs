using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

public static class DatabaseBackupContractLimits
{
    public const int IdentifierLength = 128;
    public const int SafeTextLength = 256;
    public const int DiagnosticReferenceLength = 512;
    public const int MaximumCollectionCount = 32;
    public const int MaximumPageSize = 200;
}

static class DatabaseBackupIdValidation
{
    public static string Validate(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > DatabaseBackupContractLimits.IdentifierLength)
            throw new ArgumentOutOfRangeException(parameterName, $"Identifier cannot exceed {DatabaseBackupContractLimits.IdentifierLength} characters.");
        if (normalized.Any(character => char.IsControl(character) || character is '/' or '\\'))
            throw new ArgumentException("Identifier cannot contain control characters or path separators.", parameterName);
        return normalized;
    }
}

[MessagePackObject]
public readonly record struct DatabaseRecoveryOperationId : IActorEntityId
{
    [Key(0)] public Guid Value { get; }
    [SerializationConstructor] public DatabaseRecoveryOperationId(Guid value) => Value = value;
    public string Format() => Value.ToString("N");
}

[MessagePackObject]
public readonly record struct DatabaseBackupSetId
{
    [Key(0)] public Guid Value { get; }
    [SerializationConstructor] public DatabaseBackupSetId(Guid value) => Value = value;
    public override string ToString() => Value.ToString("N");
}

[MessagePackObject]
public readonly record struct DatabaseRetentionPlanId
{
    [Key(0)] public Guid Value { get; }
    [SerializationConstructor] public DatabaseRetentionPlanId(Guid value) => Value = value;
    public override string ToString() => Value.ToString("N");
}

[MessagePackObject]
public readonly record struct DatabaseProtectionSetId
{
    [Key(0)] public string Value { get; }
    [SerializationConstructor] public DatabaseProtectionSetId(string value) => Value = DatabaseBackupIdValidation.Validate(value, nameof(value));
    public override string ToString() => Value ?? string.Empty;
}

[MessagePackObject]
public readonly record struct DatabaseRestorePointId
{
    [Key(0)] public string Value { get; }
    [SerializationConstructor] public DatabaseRestorePointId(string value) => Value = DatabaseBackupIdValidation.Validate(value, nameof(value));
    public override string ToString() => Value ?? string.Empty;
}

[MessagePackObject]
public readonly record struct DatabaseBackupPolicyId
{
    [Key(0)] public string Value { get; }
    [SerializationConstructor] public DatabaseBackupPolicyId(string value) => Value = DatabaseBackupIdValidation.Validate(value, nameof(value));
    public override string ToString() => Value ?? string.Empty;
}

[MessagePackObject]
public readonly record struct DatabaseBackupHostId
{
    [Key(0)] public string Value { get; }
    [SerializationConstructor] public DatabaseBackupHostId(string value) => Value = DatabaseBackupIdValidation.Validate(value, nameof(value));
    public override string ToString() => Value ?? string.Empty;
}

[MessagePackObject]
public readonly record struct DatabaseArtifactId
{
    [Key(0)] public string Value { get; }
    [SerializationConstructor] public DatabaseArtifactId(string value) => Value = DatabaseBackupIdValidation.Validate(value, nameof(value));
    public override string ToString() => Value ?? string.Empty;
}

[MessagePackObject]
public readonly record struct DatabaseArtifactReplicaId
{
    [Key(0)] public string Value { get; }
    [SerializationConstructor] public DatabaseArtifactReplicaId(string value) => Value = DatabaseBackupIdValidation.Validate(value, nameof(value));
    public override string ToString() => Value ?? string.Empty;
}
