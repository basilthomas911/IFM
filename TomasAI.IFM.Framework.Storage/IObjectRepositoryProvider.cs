using System.Data;

namespace TomasAI.IFM.Framework.Storage;

public interface IObjectRepositoryProvider
{
    // command methods...
    Task<long[]> ExecuteCommandAsync(IObjectRepositoryContext ctx, Action<string> onInfoMessage = null);
    object QueueCommand(string commandText, CommandType commandType, List<object> parameterValues);
    Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false);
    ValueTask ExecuteMapReduceAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> mapper, Action<IEnumerable<TResult>> reducer);

    // query methods...
    IAsyncEnumerable<TResult> StreamObjectsAsync<TResult>(
        IObjectRepositoryContext ctx,
        Func<IObjectDataRecord, TResult> mapper,
        CancellationToken cancellationToken = default);
    Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> mapper);
    Task<IReadOnlyList<TResult>> GetImmutableObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> mapper) where TResult : struct;

    Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper);

    Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TScalar> dataMapper) where TScalar : struct;

}
