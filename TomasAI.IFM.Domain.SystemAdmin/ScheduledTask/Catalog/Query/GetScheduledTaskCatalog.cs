using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Query;
/// <summary>Handles GetScheduledTaskCatalog without consulting command-state objects.</summary>
public static class GetScheduledTaskCatalog
{

    extension(GetScheduledTaskCatalogQuery query)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation targets a member of the request, but ParamName intentionally identifies the containing method argument so callers can identify the rejected request.")]
        /// <summary>Reads persisted projections or previews the supplied timing, and returns a typed result.</summary>
        public async ValueTask<ServiceResult<ScheduledTaskCatalog>> ExecuteAsync(IScheduledTaskReadStore store, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment, nameof(query));
            ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId, nameof(query));
            var result = await store.GetCatalogAsync(query.Environment, query.HostId, cancellationToken).ConfigureAwait(false);
            return result is null ? new ServiceFailed<ScheduledTaskCatalog>(404, "ScheduledTask projection was not found.") : new ServiceOk<ScheduledTaskCatalog>(result);
        }
    }
}
