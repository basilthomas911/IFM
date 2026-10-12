using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query;

public static class ListParameterVersions
{
    public static async ValueTask ExecuteAsync(this ListParameterVersionsQuery query, IParameterSetQueryContext context, ILogger<ParameterSetQueryActor> logger, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(logger);
        context.AccessPolicy.Demand(ParameterCapability.Read); token.ThrowIfCancellationRequested();
        var components = await (context.ConfigurationDb.GetParameterComponentsAsync(token)).ConfigureAwait(false);
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
        if (!components.Any(x => string.Equals(x.ComponentCode,query.ComponentCode,StringComparison.Ordinal))) throw new ArgumentException("PARAM.COMPONENT_UNSUPPORTED");
#pragma warning restore MA0015
        var result = await (context.ConfigurationDb.GetParameterSetsAsync(query.ComponentCode, query.SetId == Guid.Empty ? null : query.SetId, token, query.Limit, query.AfterName, query.AfterSetId, query.AfterVersion)).ConfigureAwait(false);
        await (context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ParameterSetVersion[]>(result))).ConfigureAwait(false);
    }
}
