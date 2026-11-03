using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Persistence;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.State;
public sealed class DatabaseBackupSetStateRepository(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService,
    IEventProjector<DatabaseBackupSetCommandActor> eventProjector, IDatabaseBackupExecutionOutbox executionOutbox,
    ILogger<DatabaseBackupSetStateRepository> logger, IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null)
    : DatabaseOperationStateRepository<DatabaseBackupSetCommandState, DatabaseBackupSetCommandActor>(stateFactory, eventSource, actorService, eventProjector, executionOutbox, logger, databases, transactions);
