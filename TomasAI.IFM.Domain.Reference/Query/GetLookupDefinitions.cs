using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query;

/// <summary>Handles <see cref="GetLookupDefinitionsQuery"/>.</summary>
public static class GetLookupDefinitions
{

    extension(GetLookupDefinitionsQuery query)
    {
        /// <summary>Reads and returns lookup definitions for the requested group.</summary>
        public async ValueTask<LookupDefinitionReadModel[]> ExecuteAsync(IReferenceQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var rows = await (context.DbFactory.ConfigurationDb.GetLookupDefinitionsAsync(query.GroupName, cancellationToken)).ConfigureAwait(false);
            return rows;
        }
    }
}
