using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging;

public interface IQueryServiceApi
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    Task<ServiceResult<TResult>> PostQueryAsync<TResult>(string uriPath, IQueryParameter queryParam, int errorCode);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(string uriPath, IQuery<TResult> query);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(string uriPath, IQueryParameter query, int errorCode);
}
