namespace TomasAI.IFM.Shared.EventSourcing;

public interface IAsyncQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> ExecuteAsync(TQuery qryParam);
}
