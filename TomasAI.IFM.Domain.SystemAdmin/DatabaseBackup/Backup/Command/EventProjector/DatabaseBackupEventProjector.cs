using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Projection;
using TomasAI.IFM.Shared.EventProjector;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.EventProjector;
public sealed class DatabaseBackupEventProjector(
    ISystemAdminDbContext db, IDurableReplayQueue queue, IEventSourceActorDbContext source,
    IBlackboardService blackboard, ILogger<DatabaseBackupEventProjector> logger, EventProjectorReliabilityOptions? options = null)
    : DatabaseOperationEventProjector<DatabaseBackupCommandActor>(db, queue, source, blackboard, logger, options);
