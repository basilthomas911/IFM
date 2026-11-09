using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Projection;
using TomasAI.IFM.Shared.EventProjector;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.EventProjector;
public sealed class DatabaseBackupPolicyEventProjector(
    ISystemAdminDbContext db, IDurableReplayQueue queue, IEventSourceActorDbContext source,
    ILogger<DatabaseBackupPolicyEventProjector> logger, EventProjectorReliabilityOptions? options = null)
    : DatabaseOperationEventProjector<DatabaseBackupPolicyCommandActor>(db, queue, source, logger, options);
