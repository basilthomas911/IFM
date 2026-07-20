using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Storage.Csv;
using TomasAI.IFM.Framework.Storage.Json;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// object data reader constructor
/// </summary>
/// <param name="db"></param>
/// <param name="options"></param>
public class ObjectDataReaderContext(IObjectRepository db, IDataReaderOptions options)
    : IObjectDataReaderContext
{
    readonly IObjectRepository _db = IsArgumentNull.Set(db);
    readonly IDataReaderOptions _options = IsArgumentNull.Set(options);

    /// <summary>
    /// read data external data by data reader type
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<ICollection<TResult>> ReadAsync<TResult>()
    {
        var resultSet = default(ICollection<TResult>);
        return _options.DataReaderType switch
        {
            DataReaderType.Csv => await GetCsvDataAsync(),
            DataReaderType.JSON => await GetJsonDataAsync(),
            _ => throw new NotImplementedException()
        };

        async ValueTask< ICollection<TResult>> GetCsvDataAsync()
        {
            if (!_db.ResultTypeMap.ContainsKey(typeof(TResult)))
                throw new InvalidOperationException($"ObjectDataReaderContext.GetCsvDataAsync: unable to map resultset to type: '{typeof(TResult).Name}'");

            var stringReader = new HttpStringReader(_options.Uri);
            using (var dataReader = new CsvDataReader<TResult>(stringReader))
            {
               resultSet = new CsvObjectDataReader<TResult>(dataReader, _db.ResultTypeMap).ReadAll();
            }
            return await ValueTask.FromResult(resultSet);
        }

        async ValueTask<ICollection<TResult>> GetJsonDataAsync()
        {
            if (!_db.ResultTypeMap.ContainsKey(typeof(TResult)))
                throw new InvalidOperationException($"ObjectDataReaderContext.GetJsonDataAsync: unable to map resultset to type: '{typeof(TResult).Name}'");

            var stringReader = new HttpStringReader(_options.Uri);
            using (var dataReader = new JsonDataReader<TResult>(stringReader))
            {
                resultSet = new JsonObjectDataReader<TResult>(dataReader, _db.ResultTypeMap).ReadAll();
            }
            return await ValueTask.FromResult(resultSet);
        }
       
    }
}
