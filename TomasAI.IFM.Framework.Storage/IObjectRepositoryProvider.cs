using System.Data;

namespace TomasAI.IFM.Framework.Storage;

public interface IObjectRepositoryProvider
{
    // command methods...
    Task<long[]> ExecuteCommandAsync(IObjectRepositoryContext ctx, Action<string> onInfoMessage = null);
    async Task<long[]> ExecuteCommandAsync(
        IObjectRepositoryContext ctx,
        CancellationToken cancellationToken,
        Action<string> onInfoMessage = null)
        => await ExecuteCommandAsync(ctx, onInfoMessage).WaitAsync(cancellationToken).ConfigureAwait(false);
    object QueueCommand(
        string commandName,
        string commandText,
        CommandType commandType,
        List<object> parameterValues);
    Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false);
    async Task ExecuteQueuedCommandsAsync(
        List<object> queuedCommands,
        bool useTransaction,
        CancellationToken cancellationToken)
        => await ExecuteQueuedCommandsAsync(queuedCommands, useTransaction)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    ValueTask ExecuteMapReduceAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> mapper, Action<IEnumerable<TResult>> reducer);
    async ValueTask ExecuteMapReduceAsync<TResult>(
        IObjectRepositoryContext ctx,
        Func<IObjectDataRecord, TResult> mapper,
        Action<IEnumerable<TResult>> reducer,
        CancellationToken cancellationToken)
        => await ExecuteMapReduceAsync(ctx, mapper, reducer)
            .AsTask()
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

    // query methods...
    IAsyncEnumerable<TResult> StreamObjectsAsync<TResult>(
        IObjectRepositoryContext ctx,
        Func<IObjectDataRecord, TResult> mapper,
        CancellationToken cancellationToken = default);
    Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> mapper);
    async Task<ICollection<TResult>> GetObjectsAsync<TResult>(
        IObjectRepositoryContext ctx,
        Func<IObjectDataRecord, TResult> mapper,
        CancellationToken cancellationToken)
        => await GetObjectsAsync(ctx, mapper).WaitAsync(cancellationToken).ConfigureAwait(false);
    Task<IReadOnlyList<TResult>> GetImmutableObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> mapper) where TResult : struct;
    async Task<IReadOnlyList<TResult>> GetImmutableObjectsAsync<TResult>(
        IObjectRepositoryContext ctx,
        Func<IObjectDataRecord, TResult> mapper,
        CancellationToken cancellationToken)
        where TResult : struct
        => await GetImmutableObjectsAsync(ctx, mapper).WaitAsync(cancellationToken).ConfigureAwait(false);

    Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper);
    async Task<TResult?> GetObjectAsync<TResult>(
        IObjectRepositoryContext ctx,
        Func<IObjectDataRecord, TResult> dataMapper,
        CancellationToken cancellationToken)
        => await GetObjectAsync(ctx, dataMapper).WaitAsync(cancellationToken).ConfigureAwait(false);

    Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TScalar> dataMapper) where TScalar : struct;
    async Task<TScalar> GetScalarAsync<TScalar>(
        IObjectRepositoryContext ctx,
        Func<IObjectDataRecord, TScalar> dataMapper,
        CancellationToken cancellationToken)
        where TScalar : struct
        => await GetScalarAsync(ctx, dataMapper).WaitAsync(cancellationToken).ConfigureAwait(false);

}
