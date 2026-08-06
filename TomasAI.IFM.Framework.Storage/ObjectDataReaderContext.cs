using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Storage.Csv;
using TomasAI.IFM.Framework.Storage.Json;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// object data reader constructor
/// </summary>
/// <param name="db"></param>
/// <param name="options"></param>
public class ObjectDataReaderContext : IObjectDataReaderContext
{
    readonly IDataReaderOptions _options;

    public ObjectDataReaderContext(IObjectRepository db, IDataReaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(db);
        _options = IsArgumentNull.Set(options);
    }

    /// <summary>
    /// read data external data by data reader type
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<ICollection<TResult>> ReadAsync<TResult>(Func<IObjectDataRecord, TResult> mapper)
        => await ReadAsync(mapper, CancellationToken.None).ConfigureAwait(false);

    public async Task<ICollection<TResult>> ReadAsync<TResult>(
        Func<IObjectDataRecord, TResult> mapper,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        cancellationToken.ThrowIfCancellationRequested();
        var httpReader = new HttpStringReader(_options.Uri);
        var content = await httpReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stringReader = new BufferedStringReader(content);
        var resultSet = default(ICollection<TResult>);
        return _options.DataReaderType switch
        {
            DataReaderType.Csv => await GetCsvDataAsync(),
            DataReaderType.JSON => await GetJsonDataAsync(),
            _ => throw new NotImplementedException()
        };

        async ValueTask< ICollection<TResult>> GetCsvDataAsync()
        {
            using var dataReader = new CsvDataReader<TResult>(stringReader);
            resultSet = ReadAll(dataReader, cancellationToken);
            return await ValueTask.FromResult(resultSet);
        }

        async ValueTask<ICollection<TResult>> GetJsonDataAsync()
        {
            using var dataReader = new JsonDataReader<TResult>(stringReader);
            resultSet = ReadAll(dataReader, cancellationToken);
            return await ValueTask.FromResult(resultSet);
        }

        ICollection<TResult> ReadAll(
            System.Data.IDataReader dataReader,
            CancellationToken readCancellationToken)
        {
            List<TResult> results = [];
            var record = new AdoNetDataRecord().SetReader(dataReader);
            while (dataReader.Read())
            {
                readCancellationToken.ThrowIfCancellationRequested();
                results.Add(mapper(record));
            }
            return results;
        }
    }

    sealed class BufferedStringReader(string content) : IStringReader
    {
        public Task<string> ReadToEndAsync()
            => Task.FromResult(content);

        public Task<string> ReadToEndAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(content);
        }

        public IAsyncEnumerable<string> ReadLinesAsync()
            => ReadLinesAsync(CancellationToken.None);

        public async IAsyncEnumerable<string> ReadLinesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var line in content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
            }

            await Task.CompletedTask;
        }
    }
}
