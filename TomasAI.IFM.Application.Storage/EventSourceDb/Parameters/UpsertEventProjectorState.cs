using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertEventProjectorState(
    long eventId,
    string actorName,
    string projectorName,
    bool isReplay,
    int attemptNumber,
    string outcome,
    string stage,
    string errorMessage,
    string createdTimestamp,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(actorName),
        Text(projectorName),
        Boolean(isReplay),
        Integer(attemptNumber),
        Text(outcome),
        Text(stage),
        Text(errorMessage),
        Text(createdTimestamp),
        Text(updatedTimestamp));
}
