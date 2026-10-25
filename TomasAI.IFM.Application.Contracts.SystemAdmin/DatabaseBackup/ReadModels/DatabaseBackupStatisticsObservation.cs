using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
/// <summary>One persisted engine/phase statistics observation for domain query composition.</summary>
public sealed record DatabaseBackupStatisticsObservation(BackupSource Source, long Revision, DatabaseRecoveryRunStatistics Statistics);
