namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
public enum DatabaseRecoveryOperationKind { None = 0, Backup = 1, Verification = 2, Restore = 3, RestoreDrill = 4, Cutover = 5, Reconciliation = 6, Retention = 7 }
