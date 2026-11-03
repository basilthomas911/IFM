using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Persistence;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;
public sealed class DatabaseBackupRetentionStateRepository(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService,
    IEventProjector<DatabaseBackupRetentionCommandActor> eventProjector, IDatabaseBackupExecutionOutbox executionOutbox,
    ILogger<DatabaseBackupRetentionStateRepository> logger, IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null)
    : DatabaseOperationStateRepository<DatabaseBackupRetentionCommandState, DatabaseBackupRetentionCommandActor>(stateFactory, eventSource, actorService, eventProjector, executionOutbox, logger, databases, transactions);
