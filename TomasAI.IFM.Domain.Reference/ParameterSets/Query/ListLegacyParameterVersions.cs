using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query;

public static class ListLegacyParameterVersions
{

    extension(ListLegacyParameterVersionsQuery query)
    {
        public async ValueTask<ParameterLegacyVersion[]> ExecuteAsync(
            IParameterSetQueryContext context,
            ILogger<ParameterSetQueryActor> logger,
            CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            context.AccessPolicy.Demand(ParameterCapability.Read);
            var rows = await (context.ConfigurationDb.GetLegacyParameterVersionsAsync(offset: query.Offset, cancellationToken: token)).ConfigureAwait(false);
            return rows;
        }
    }
}
