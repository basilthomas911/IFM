using TomasAI.IFM.Shared.Exceptions;

using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventSourcing;

public abstract class BaseQueryHandler
{
    public static async Task<TResult> ExecuteQueryAsync<TResult>(IQuery<TResult> q, Func<Task<TResult>> queryFunc)
    {
        ArgumentNullException.ThrowIfNull(q);
        ArgumentNullException.ThrowIfNull(queryFunc);
        TResult result;
        try
        {
            result = await queryFunc().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new QueryException(q.ErrorCode, ex.Message, ex.InnerException!);
        }
        return result;
    }
}
