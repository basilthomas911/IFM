using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Query;

/// <summary>
/// 
/// </summary>
public interface IQueryService
{
    Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(IQuery<TResult> query) where TResult : class;
}
