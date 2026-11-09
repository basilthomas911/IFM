using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Projection;
using TomasAI.IFM.Shared.EventProjector;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.EventProjector;
public sealed class DatabaseRecoveryEventProjector(
    ISystemAdminDbContext db, IDurableReplayQueue queue, IEventSourceActorDbContext source,
    ILogger<DatabaseRecoveryEventProjector> logger, EventProjectorReliabilityOptions? options = null)
    : DatabaseOperationEventProjector<DatabaseRecoveryCommandActor>(db, queue, source, logger, options);
