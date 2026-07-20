using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging
{
    public interface IQueryService
    {
        Task<ServiceResult<TResult>> PostApiQueryAsync<TResult>(IQuery<TResult> query, string controllerName);
        Task<ServiceResult<TResult>> ExecuteApiQueryAsync<TResult>(IQuery<TResult> query, string controllerName);
    }
}
