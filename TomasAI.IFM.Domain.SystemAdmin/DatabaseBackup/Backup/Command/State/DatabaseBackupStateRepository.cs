using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Persistence;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.State;
public sealed class DatabaseBackupStateRepository(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService,
    IEventProjector<DatabaseBackupCommandActor> eventProjector, IDatabaseBackupExecutionOutbox executionOutbox,
    ILogger<DatabaseBackupStateRepository> logger, IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null)
    : DatabaseOperationStateRepository<DatabaseBackupCommandState, DatabaseBackupCommandActor>(stateFactory, eventSource, actorService, eventProjector, executionOutbox, logger, databases, transactions);
