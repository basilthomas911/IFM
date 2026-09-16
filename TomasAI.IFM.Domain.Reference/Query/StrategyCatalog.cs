using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query;

/// <summary>Handles <see cref="StrategyCatalogQuery"/>.</summary>
public static class StrategyCatalog
{
    /// <summary>Executes a typed Strategy Catalog request and returns its JSON result.</summary>
    public static async ValueTask ExecuteAsync(this StrategyCatalogQuery query, IReferenceQueryContext context, CancellationToken cancellationToken)
    {
        var request = StrategyCatalogJson.Read<CatalogQueryRequest>(query.RequestJson);
        var value = await new TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogService(context.DbFactory).QueryAsync(request, cancellationToken);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<string>(value));
    }
}