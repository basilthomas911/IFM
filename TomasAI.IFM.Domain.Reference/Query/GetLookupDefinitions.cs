using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query;

/// <summary>Handles <see cref="GetLookupDefinitionsQuery"/>.</summary>
public static class GetLookupDefinitions
{
    /// <summary>Reads and replies with lookup definitions for the requested group.</summary>
    public static async ValueTask ExecuteAsync(this GetLookupDefinitionsQuery query, IReferenceQueryContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        var rows = await (context.DbFactory.ConfigurationDb.GetLookupDefinitionsAsync(query.GroupName, cancellationToken)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await (context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<LookupDefinitionReadModel[]>(rows))).ConfigureAwait(false);
    }
}