using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseRequestEnvelope : IDatabaseBackupValidatable
{
    public const int CurrentContractVersion = 1;

    [Key(0)] public int ContractVersion { get; init; } = CurrentContractVersion;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public string CallerIdentity { get; init; } = string.Empty;
    [Key(3)] public string AuthorizationReference { get; init; } = string.Empty;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(4)] public string[] CallerRoles { get; init; } = [];
    [Key(5)] public DatabaseRequestOrigin Origin { get; init; }
    [Key(6)] public Guid CorrelationId { get; init; }
    [Key(7)] public Guid CausationId { get; init; }
    [Key(8)] public string EnvironmentIdentity { get; init; } = string.Empty;
    [Key(9)] public DateTimeOffset CreatedUtc { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "This validates persisted instance properties, not method arguments; the property name identifies the invalid stored field.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This validates persisted instance properties, not method arguments; the property name identifies the invalid stored field.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This validates persisted instance properties, not method arguments; the property name identifies the invalid stored field.")]
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
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName);
        if (value.Length > maximumLength || value.Any(char.IsControl)) throw new ArgumentOutOfRangeException(parameterName);
    }

    internal static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value == default || value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be a non-default UTC DateTimeOffset.", parameterName);
    }
}
