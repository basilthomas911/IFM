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
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var resultSet = default(ICollection<TResult>);
        return _options.DataReaderType switch
        {
            DataReaderType.Csv => await GetCsvDataAsync(),
            DataReaderType.JSON => await GetJsonDataAsync(),
            _ => throw new NotImplementedException()
        };

        async ValueTask< ICollection<TResult>> GetCsvDataAsync()
        {
            var stringReader = new HttpStringReader(_options.Uri);
            using var dataReader = new CsvDataReader<TResult>(stringReader);
            resultSet = ReadAll(dataReader);
            return await ValueTask.FromResult(resultSet);
        }

        async ValueTask<ICollection<TResult>> GetJsonDataAsync()
        {
            var stringReader = new HttpStringReader(_options.Uri);
            using var dataReader = new JsonDataReader<TResult>(stringReader);
            resultSet = ReadAll(dataReader);
            return await ValueTask.FromResult(resultSet);
        }

        ICollection<TResult> ReadAll(System.Data.IDataReader dataReader)
        {
            List<TResult> results = [];
            var record = new AdoNetDataRecord().SetReader(dataReader);
            while (dataReader.Read())
                results.Add(mapper(record));
            return results;
        }
    }
}
