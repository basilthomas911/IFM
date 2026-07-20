using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Shared.EventSourcing;

public abstract class BaseQueryHandler
{
    public static async Task<TResult> ExecuteQueryAsync<TResult>(IQuery<TResult> q, Func<Task<TResult>> queryFunc)
    {
        TResult result;
        try
        {
            result = await queryFunc();
        }
        catch (Exception ex)
        {
            throw new QueryException(q.ErrorCode, ex.Message, ex.InnerException!);
        }
        return result;
    }
}
