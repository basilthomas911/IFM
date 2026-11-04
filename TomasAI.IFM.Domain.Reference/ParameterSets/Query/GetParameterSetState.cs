using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query;

public static class GetParameterSetState
{

    extension(GetParameterSetStateQuery query)
    {
        public async ValueTask<ParameterSetSnapshot> ExecuteAsync(IParameterSetQueryContext context, ILogger<ParameterSetQueryActor> logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(logger);
            context.AccessPolicy.Demand(ParameterCapability.Read); token.ThrowIfCancellationRequested();
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            if (query.SetId == Guid.Empty) throw new ArgumentException("PARAM.IDENTITY_INVALID");
#pragma warning restore MA0015
            return await context.ConfigurationDb.GetParameterSetSnapshotAsync(query.SetId, token).ConfigureAwait(false);
        }
    }
}
