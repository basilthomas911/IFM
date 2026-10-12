using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
internal sealed record ScheduledJobWriteOperation(
    Guid OperationId,
    DateTime StartedOn,
    List<ScheduledJobWriteOwnership> Ownerships);
