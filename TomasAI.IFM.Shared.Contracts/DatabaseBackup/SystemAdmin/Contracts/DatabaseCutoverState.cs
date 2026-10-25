namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
public enum DatabaseCutoverState { None = 0, NotRequested = 1, AwaitingApproval = 2, Approved = 3, InProgress = 4, Completed = 5, Rejected = 6 }
