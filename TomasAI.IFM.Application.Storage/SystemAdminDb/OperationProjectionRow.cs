using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

internal sealed record OperationProjectionRow(
    DatabaseBackupOperationReadModel Operation,
    DatabaseRestorePointId? RestorePointId,
    DatabaseRestoreClass RestoreClass,
    string FreshTargetProfile,
    long ValidationRevision,
    DatabaseCutoverState CutoverState);
