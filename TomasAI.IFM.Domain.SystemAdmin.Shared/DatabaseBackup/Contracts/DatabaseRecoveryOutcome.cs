namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
public enum DatabaseRecoveryOutcome { None = 0, Succeeded = 1, Failed = 2, Cancelled = 3, Rejected = 4, Degraded = 5 }
