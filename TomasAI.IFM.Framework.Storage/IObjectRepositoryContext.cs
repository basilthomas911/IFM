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
    string CommandText { get; }

    IObjectRepositoryContext SetParameters(object parameterValue = default);
    IObjectRepositoryContext SetParameters<TParam>(in TParam parameterValue) where TParam : struct, IBindValue;
    IObjectRepositoryContext SetParameters<TParam>(IEnumerable<TParam> parameterValues);
    void SetCommand(IDbCommand cmd);
    string GetParameterName(string parameterName);

    object QueueCommand();

    // async methods...
    Task<ICollection<TResult>> ExecuteQueryAsync<TResult>(Func<IObjectMapReader<TResult>, TResult> dataMapper);
    Task<ICollection<TResult>> ExecuteQueryAsync<TResult>(Func<string, int, TResult> mapper);
    Task<ICollection<TResult>> ExecuteQueryAsync<TResult>(Func<IObjectDataRecord, TResult> dataMapper);
    Task<IReadOnlyList<TResult>> ExecuteQueryImmutableAsync<TResult>(Func<IObjectDataRecord, TResult> dataMapper) where TResult : struct;
    Task<TResult?> ExecuteSingleAsync<TResult>(Func<IObjectMapReader<TResult>, TResult> dataReaderMapper);
    Task<TResult?> ExecuteSingleAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper);
    Task<TResult?> ExecuteSingleAsync<TResult>(IObjectDataRecord dataReaderMapper);
    Task<TResult> ExecuteScalarAsync<TResult>(Func<IObjectMapReader<Scalar<TResult>>, TResult> dataReaderMapper) where TResult : struct;
    Task<TResult> ExecuteScalarAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper) where TResult : struct;

    Task<long[]> ExecuteCommandAsync(Action<string> onInfoMessage = default!);
    Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false);

    ValueTask ExecuteMapReduceAsync<TResult>(Func<IObjectMapReader<TResult>, TResult> mapper, Action<IEnumerable<TResult>> reducer);
    ValueTask ExecuteMapReduceAsync<TResult>(Func<IObjectDataRecord, TResult> mapper, Action<IEnumerable<TResult>> reducer);
}
