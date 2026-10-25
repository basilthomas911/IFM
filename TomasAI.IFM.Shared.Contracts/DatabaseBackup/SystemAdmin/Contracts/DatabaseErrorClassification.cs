namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
public enum DatabaseErrorClassification { None = 0, Retryable = 1, OperatorActionable = 2, Terminal = 3 }
