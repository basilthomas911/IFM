using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query;

public static class ListLegacyParameterVersions
{
    public static async ValueTask ExecuteAsync(
        this ListLegacyParameterVersionsQuery query,
        IParameterSetQueryContext context,
        ILogger<ParameterSetQueryActor> logger,
        CancellationToken token)
    {
        context.AccessPolicy.Demand(ParameterCapability.Read);
        var rows = await context.ConfigurationDb.ReadLegacyParameterVersionsAsync(offset: query.Offset, token: token);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ParameterLegacyVersion[]>(rows));
    }
}