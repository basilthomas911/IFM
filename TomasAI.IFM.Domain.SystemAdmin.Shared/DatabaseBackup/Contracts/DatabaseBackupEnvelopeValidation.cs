using FluentValidation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

/// <summary>Nonthrowing validation for shared backup envelopes and identifiers.</summary>
public static class DatabaseBackupEnvelopeValidation
{
    private static readonly RequestRules Requests = new();
    private static readonly SourceRules Sources = new();

    /// <summary>Validates the recovery operation's intrinsic identity.</summary>
    public static List<ValidationError> ValidateRecoveryOperationId(this List<ValidationError> errors, DatabaseRecoveryOperationId id, string commandName)
    {
        if (id.Value == Guid.Empty) errors.Add(new($"{commandName}.EntityId is required."));
        return errors;
    }

    /// <summary>Appends every request envelope failure, including missing envelopes.</summary>
    public static List<ValidationError> ValidateBackupRequest(this List<ValidationError> errors, DatabaseRequestEnvelope? request)
    {
        if (request is null) errors.Add(new("Request is required."));
        else errors.AddRange(Requests.Validate(request).Errors.Select(static failure => new ValidationError(failure.ErrorMessage)));
        return errors;
    }

    /// <summary>Appends every source envelope failure, including missing envelopes.</summary>
    public static List<ValidationError> ValidateBackupSource(this List<ValidationError> errors, DatabaseSourceEnvelope? source)
    {
        if (source is null) errors.Add(new("Source is required."));
        else errors.AddRange(Sources.Validate(source).Errors.Select(static failure => new ValidationError(failure.ErrorMessage)));
        return errors;
    }

    /// <summary>Checks a bounded identifier without constructing a throwing identifier wrapper.</summary>
    public static bool IsIdentifier(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Trim().Length <= DatabaseBackupContractLimits.IdentifierLength &&
        !value.Any(character => char.IsControl(character) || character is '/' or '\\');

    /// <summary>Checks bounded text used in approval and diagnostic fields.</summary>
    public static bool IsSafeText(string? value, int maximumLength = DatabaseBackupContractLimits.SafeTextLength, bool required = true)
        => value is not null && (!required || !string.IsNullOrWhiteSpace(value)) && value.Length <= maximumLength && !value.Any(char.IsControl);

    private sealed class RequestRules : AbstractValidator<DatabaseRequestEnvelope>
    {
        public RequestRules()
        {
            RuleFor(request => request.ContractVersion).Equal(DatabaseRequestEnvelope.CurrentContractVersion);
            RuleFor(request => request.RequestId).NotEmpty();
            RuleFor(request => request.CallerIdentity).Must(value => IsSafeText(value));
            RuleFor(request => request.AuthorizationReference).Must(value => IsSafeText(value));
            RuleFor(request => request.CallerRoles).NotNull().Must(roles => roles is null || roles.Length <= DatabaseBackupContractLimits.MaximumCollectionCount);
            When(request => request.CallerRoles is not null, () => RuleForEach(request => request.CallerRoles).Must(value => IsSafeText(value)));
            RuleFor(request => request.Origin).IsInEnum().NotEqual(DatabaseRequestOrigin.None);
            RuleFor(request => request.EnvironmentIdentity).Must(value => IsSafeText(value));
            RuleFor(request => request.CreatedUtc).Must(time => time != default && time.Offset == TimeSpan.Zero);
        }
    }

    private sealed class SourceRules : AbstractValidator<DatabaseSourceEnvelope>
    {
        public SourceRules()
        {
            RuleFor(source => source.ContractVersion).Equal(DatabaseRequestEnvelope.CurrentContractVersion);
            RuleFor(source => source.SourceEventId).NotEmpty();
            RuleFor(source => source.OperationId.Value).NotEmpty();
            RuleFor(source => source.Source).Must(source => source is BackupSource.LocalWorkstation or BackupSource.AwsCloud);
            RuleFor(source => source.ProtectionSetId.Value).Must(IsIdentifier);
            RuleFor(source => source.PolicyRevision).GreaterThanOrEqualTo(0);
            RuleFor(source => source.OperationKind).IsInEnum().NotEqual(DatabaseRecoveryOperationKind.None);
            RuleFor(source => source.Phase).IsInEnum().NotEqual(DatabaseRecoveryPhase.None);
            RuleFor(source => source.SourceRevisionOrSequence).GreaterThanOrEqualTo(0);
            RuleFor(source => source.ObservedUtc).Must(time => time != default && time.Offset == TimeSpan.Zero);
        }
    }
}
