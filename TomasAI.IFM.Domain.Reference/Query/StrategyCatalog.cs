using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query;

/// <summary>Handles <see cref="StrategyCatalogQuery"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1724", Justification = "The typed command/query extension name follows its published message family; the storage namespace is a separate contract.")]
public static class StrategyCatalog
{

    extension(StrategyCatalogQuery query)
    {
        /// <summary>Executes a typed Strategy Catalog request and returns its JSON result.</summary>
        public async ValueTask<string> ExecuteAsync(IReferenceQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var request = StrategyCatalogJson.Read<CatalogQueryRequest>(query.RequestJson);
            var value = await (new TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogService(context.DbFactory).QueryAsync(request, cancellationToken)).ConfigureAwait(false);
            return value;
        }
    }
}