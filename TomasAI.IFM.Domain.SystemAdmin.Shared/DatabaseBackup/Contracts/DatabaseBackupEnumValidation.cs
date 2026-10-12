namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

public static class DatabaseBackupEnumValidation
{
    public static void RequireConcrete(BackupSource source, string parameterName = "source")
    {
        if (source is not (BackupSource.LocalWorkstation or BackupSource.AwsCloud))
            throw new ArgumentOutOfRangeException(parameterName, source, "A concrete, supported backup source is required.");
    }

    public static void RequireDefined<TEnum>(TEnum value, string parameterName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value) || Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) == 0)
            throw new ArgumentOutOfRangeException(parameterName, value, "A defined non-zero value is required.");
    }

    public static void RequireOptionalDefined<TEnum>(TEnum value, string parameterName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(parameterName, value, "A defined value is required.");
    }
}
