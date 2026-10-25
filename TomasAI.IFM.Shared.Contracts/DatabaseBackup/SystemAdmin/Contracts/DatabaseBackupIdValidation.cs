using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

static class DatabaseBackupIdValidation
{
    public static string Validate(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > DatabaseBackupContractLimits.IdentifierLength)
            throw new ArgumentOutOfRangeException(parameterName, $"Identifier cannot exceed {DatabaseBackupContractLimits.IdentifierLength} characters.");
        if (normalized.Any(character => char.IsControl(character) || character is '/' or '\\'))
            throw new ArgumentException("Identifier cannot contain control characters or path separators.", parameterName);
        return normalized;
    }
}
