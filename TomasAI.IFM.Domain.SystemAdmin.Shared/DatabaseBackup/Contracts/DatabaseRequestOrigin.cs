namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
public enum DatabaseRequestOrigin { None = 0, UI = 1, Console = 2, ScheduledTask = 3, Reconciliation = 4 }
