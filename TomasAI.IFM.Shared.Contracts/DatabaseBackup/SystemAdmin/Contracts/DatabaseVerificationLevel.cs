namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
public enum DatabaseVerificationLevel { None = 0, Checksum = 1, Native = 2, IsolatedRestore = 3, ApplicationValidation = 4 }
