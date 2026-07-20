using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging;

public interface IQueryServiceApi
{
    Task<ServiceResult<TResult>> PostQueryAsync<TResult>(string uriPath, IQueryParameter queryParam, int errorCode);
    Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(string uriPath, IQuery<TResult> query);
    Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(string uriPath, IQueryParameter query, int errorCode);
}
