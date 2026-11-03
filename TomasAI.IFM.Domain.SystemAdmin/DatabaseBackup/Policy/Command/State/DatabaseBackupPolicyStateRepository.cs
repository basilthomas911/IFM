using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Persistence;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.State;
public sealed class DatabaseBackupPolicyStateRepository(
    IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService,
    IEventProjector<DatabaseBackupPolicyCommandActor> eventProjector, IDatabaseBackupExecutionOutbox executionOutbox,
    ILogger<DatabaseBackupPolicyStateRepository> logger, IDbContextFactory? databases = null, IPostgresEventTransaction? transactions = null)
    : DatabaseOperationStateRepository<DatabaseBackupPolicyCommandState, DatabaseBackupPolicyCommandActor>(stateFactory, eventSource, actorService, eventProjector, executionOutbox, logger, databases, transactions);
