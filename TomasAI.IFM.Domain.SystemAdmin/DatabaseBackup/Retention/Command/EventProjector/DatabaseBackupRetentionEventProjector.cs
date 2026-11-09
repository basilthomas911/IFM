using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Projection;
using TomasAI.IFM.Shared.EventProjector;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.EventProjector;
public sealed class DatabaseBackupRetentionEventProjector(
    ISystemAdminDbContext db, IDurableReplayQueue queue, IEventSourceActorDbContext source,
    ILogger<DatabaseBackupRetentionEventProjector> logger, EventProjectorReliabilityOptions? options = null)
    : DatabaseOperationEventProjector<DatabaseBackupRetentionCommandActor>(db, queue, source, logger, options);
