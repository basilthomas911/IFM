using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;

namespace TomasAI.IFM.Framework.Storage;

public interface IObjectRepositoryContext: IDisposable
{
    IObjectRepository Repository { get; }
    List<object> ParameterValues { get; }
    bool UseTransaction { get; }
    int CommandTimeout { get; }
    string CommandName { get; }
    string CommandText { get; }
    string CommandLogText { get; }

    IObjectRepositoryContext SetParameters(object parameterValue = default);
    IObjectRepositoryContext SetParameters<TParam>(in TParam parameterValue) where TParam : struct, IBindValue;
    IObjectRepositoryContext SetParameters<TParam>(TParam[] parameterValues)
        where TParam : struct, IBindValue
        => SetParameters((IEnumerable<TParam>)parameterValues);
    IObjectRepositoryContext SetParameters<TParam>(IReadOnlyList<TParam> parameterValues)
        where TParam : struct, IBindValue
        => SetParameters((IEnumerable<TParam>)parameterValues);
    IObjectRepositoryContext SetParameters<TParam>(IEnumerable<TParam> parameterValues);
    void SetCommand(IDbCommand cmd);
    string GetParameterName(string parameterName);

    object QueueCommand();

    // async methods...
    IAsyncEnumerable<TResult> ExecuteStreamAsync<TResult>(
        Func<IObjectDataRecord, TResult> dataMapper,
        CancellationToken cancellationToken = default);
    Task<ICollection<TResult>> ExecuteQueryAsync<TResult>(Func<IObjectDataRecord, TResult> dataMapper);
    Task<ICollection<TResult>> ExecuteQueryAsync<TResult>(Func<IObjectDataRecord, TResult> dataMapper, CancellationToken cancellationToken);
    Task<IReadOnlyList<TResult>> ExecuteQueryImmutableAsync<TResult>(Func<IObjectDataRecord, TResult> dataMapper) where TResult : struct;
    Task<IReadOnlyList<TResult>> ExecuteQueryImmutableAsync<TResult>(Func<IObjectDataRecord, TResult> dataMapper, CancellationToken cancellationToken) where TResult : struct;
    Task<TResult?> ExecuteSingleAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper);
    Task<TResult?> ExecuteSingleAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper, CancellationToken cancellationToken);
    Task<TResult> ExecuteScalarAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper) where TResult : struct;
    Task<TResult> ExecuteScalarAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper, CancellationToken cancellationToken) where TResult : struct;

    Task<long[]> ExecuteCommandAsync(Action<string> onInfoMessage = default!);
    Task<long[]> ExecuteCommandAsync(CancellationToken cancellationToken, Action<string> onInfoMessage = default!);
    Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false);
    Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction, CancellationToken cancellationToken);

    ValueTask ExecuteMapReduceAsync<TResult>(Func<IObjectDataRecord, TResult> mapper, Action<IEnumerable<TResult>> reducer);
    ValueTask ExecuteMapReduceAsync<TResult>(Func<IObjectDataRecord, TResult> mapper, Action<IEnumerable<TResult>> reducer, CancellationToken cancellationToken);
}
