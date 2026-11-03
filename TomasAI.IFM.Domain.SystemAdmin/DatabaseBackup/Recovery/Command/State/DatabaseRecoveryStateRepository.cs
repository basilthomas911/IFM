using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Persistence;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
public sealed class DatabaseRecoveryStateRepository(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService,
    IEventProjector<DatabaseRecoveryCommandActor> eventProjector, IDatabaseBackupExecutionOutbox executionOutbox,
    ILogger<DatabaseRecoveryStateRepository> logger, IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null)
    : DatabaseOperationStateRepository<DatabaseRecoveryCommandState, DatabaseRecoveryCommandActor>(stateFactory, eventSource, actorService, eventProjector, executionOutbox, logger, databases, transactions);
