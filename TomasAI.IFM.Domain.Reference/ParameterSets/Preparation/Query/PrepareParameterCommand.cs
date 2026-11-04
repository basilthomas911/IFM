using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query;

/// <summary>Adapts the original preparation reply for callers using the compatibility route.</summary>
public static class PrepareParameterCommand
{
    extension(PrepareParameterCommandQuery query)
    {
        public async ValueTask<ParameterCommandPreparation> ExecuteAsync(IParameterPreparationQueryContext context, ILogger<ParameterPreparationQueryActor> logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            var evidence = await GetParameterCommandEvidence.ReadAsync(query.Preparation, context, logger, token).ConfigureAwait(false);
            return query.Preparation.WithEvidence(evidence);
        }
    }
}
