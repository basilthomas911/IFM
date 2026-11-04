using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query;

public static class ValidateTypedParameterCandidate
{
    extension(ValidateTypedParameterCandidateQuery query)
    {
        public async ValueTask ExecuteAsync(IParameterSetQueryContext context,
            ILogger<ParameterSetQueryActor> logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query); ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(logger);
            context.AccessPolicy.Demand(ParameterCapability.Read); token.ThrowIfCancellationRequested();
            var report = TypedParameterDraftModel.Validate(query.ComponentCode, query.SchemaVersion, query.Parameters);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ParameterValidationReport>(report)).ConfigureAwait(false);
        }
    }
}
