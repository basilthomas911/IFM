using System.Data;
using System.IO;
using TomasAI.IFM.Framework.Storage.Csv;
using TomasAI.IFM.Framework.Storage.Json;

namespace TomasAI.IFM.Framework.Storage;

public class ObjectFileUriContext(Uri uri, IDataReaderOptions dataReaderOptions) 
    : ObjectUriContext(uri, dataReaderOptions), IObjectFileUriContext
{

    public void Read<TResult>(Func<string, TResult> mapper, Action<IEnumerable<TResult>> reducer)
    {
       
    }

    /// <summary>
    ///  read data from a source and maps it to a collection of results using the specified mapper function.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="dataReaderMapper"></param>
    /// <returns></returns>
    public async Task<ICollection<TResult>> ReadAsync<TResult>(Func<string, TResult> dataReaderMapper)
    {
        ICollection<TResult> resultSet = [];
        var stringReader = new FileStringReader(Uri);
        await foreach(var line in stringReader.ReadLinesAsync())
        {
            try
            {
                var result = dataReaderMapper(line);
                if (result is not null)
                    resultSet.Add(result);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"Error reading data: {ex.Message}");
            }
        }
        return resultSet;
    }

    /// <summary>
    /// Asynchronously reads data from a source and maps it to a collection of results using the specified mapper
    /// function.
    /// </summary>
    /// <remarks>The method supports multiple data reader types, such as CSV and JSON, as specified by the
    /// <see cref="DataReaderOptions.DataReaderType"/> property. The caller must ensure that the <paramref
    /// name="dataReaderMapper"/> function is capable of handling the data structure provided by the source.</remarks>
    /// <typeparam name="TResult">The type of the objects to be returned in the result collection.</typeparam>
    /// <param name="dataReaderMapper">A function that maps data from an <see cref="IObjectMapReader{TResult}"/> to an instance of <typeparamref
    /// name="TResult"/>. This function is invoked for each record in the data source.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of <typeparamref
    /// name="TResult"/>  objects representing the mapped data.</returns>
    /// <exception cref="NotImplementedException">Thrown if the <see cref="DataReaderOptions.DataReaderType"/> is not supported.</exception>
    public async Task<ICollection<TResult>> ReadAsync<TResult>(Func<IObjectMapReader<TResult>, TResult> dataReaderMapper)
    {
        return DataReaderOptions.DataReaderType switch
        {
            DataReaderType.Csv => await GetCsvDataAsync(),
            DataReaderType.JSON => await GetJsonDataAsync(),
            _ => throw new NotImplementedException()
        };

        async ValueTask<ICollection<TResult>> GetCsvDataAsync()
        {
            var stringReader = new FileStringReader(Uri);
            using var dataReader = new CsvDataReader<TResult>(stringReader);
            var resultSet = ReadAll(dataReader);
            return await ValueTask.FromResult(resultSet);
        }

        async ValueTask<ICollection<TResult>> GetJsonDataAsync()
        {
            var stringReader = new HttpStringReader(Uri);
            using var dataReader = new JsonDataReader<TResult>(stringReader);
            var resultSet = ReadAll(dataReader);
            return await ValueTask.FromResult(resultSet);
        }

        ICollection<TResult> ReadAll(IDataReader  dataReader)
        {
            var resultSet = new List<TResult>();
            var objectDataMapReader = new ObjectDataMapReader<TResult>(dataReader);
            while (dataReader.Read())
            {
                try
                {

                    var result = dataReaderMapper(objectDataMapReader);
                    if (result is not null)
                        resultSet.Add(result);
                }
                catch (Exception ex)
                {
                    // Log the exception or handle it as needed
                    Console.WriteLine($"Error reading data: {ex.Message}");
                }
            }
            return resultSet;
        }

    }
}

public static class ObjectFileUriContextExtensions
{
    /// <summary>
    /// Asynchronously reads data from the specified <see cref="IObjectUriContext"/> and maps it to a collection of
    /// results.
    /// </summary>
    /// <typeparam name="TResult">The type of the objects to be read and mapped.</typeparam>
    /// <param name="uriCtx">The context representing the source of the data to be read. Must be an instance of <see
    /// cref="ObjectFileUriContext"/>.</param>
    /// <param name="dataReaderMapper">A function that maps an <see cref="IObjectMapReader{TResult}"/> to an instance of <typeparamref
    /// name="TResult"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of <typeparamref
    /// name="TResult"/> objects mapped from the data source.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="uriCtx"/> is not an instance of <see cref="ObjectFileUriContext"/>.</exception>
    public static async Task<ICollection<TResult>> ReadAsync<TResult>(this IObjectUriContext uriCtx, Func<IObjectMapReader<TResult>, TResult> dataReaderMapper)
    {
        if (uriCtx is not ObjectFileUriContext objectFileUriContext)
        {
            throw new ArgumentException("The provided context is not an instance of ObjectFileUriContext.", nameof(uriCtx));
        }
        return await objectFileUriContext.ReadAsync(dataReaderMapper);
    }

    public static async Task<ICollection<TResult>> ReadAsync<TResult>(this IObjectUriContext uriCtx, Func<string, TResult> dataReaderMapper)
    {
        if (uriCtx is not ObjectFileUriContext objectFileUriContext)
        {
            throw new ArgumentException("The provided context is not an instance of ObjectFileUriContext.", nameof(uriCtx));
        }
        return await objectFileUriContext.ReadAsync(dataReaderMapper);
    }

    public static async Task ReadAsync<TResult>(this IObjectUriContext uriCtx, Func<string, int, TResult> mapper, Func<IEnumerable<TResult>,  Task> reducer)
    {
        if (uriCtx is not ObjectFileUriContext objectFileUriContext)
        {
            throw new ArgumentException("The provided context is not an instance of ObjectFileUriContext.", nameof(uriCtx));
        }
        var stringReader = new FileStringReader(uriCtx.Uri);
        await reducer!.Invoke(GetResultSet());

        IEnumerable<TResult> GetResultSet()
        {
            
            var readLinesTask = stringReader.ReadLinesAsync().GetAsyncEnumerator();
            while (readLinesTask.MoveNextAsync().AsTask().Result)
            {
                var start = 0;
                var row = readLinesTask.Current;
                var result = mapper(row, start);
                if (result is not null)
                    yield return result;
            }
            readLinesTask.DisposeAsync().AsTask().Wait();
        }
    }
}
