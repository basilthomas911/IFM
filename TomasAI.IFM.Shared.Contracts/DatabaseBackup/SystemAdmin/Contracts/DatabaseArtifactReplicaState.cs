namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
public enum DatabaseArtifactReplicaState { None = 0, Planned = 1, Staging = 2, Transferring = 3, Durable = 4, Verified = 5, Published = 6, Failed = 7, Deleted = 8 }
