using System.Data;
using System.IO;
using TomasAI.IFM.Framework.Storage.Csv;
using TomasAI.IFM.Framework.Storage.Json;

namespace TomasAI.IFM.Framework.Storage;

public class ObjectFileUriContext(string commandName, Uri uri, IDataReaderOptions dataReaderOptions)
    : ObjectUriContext(commandName, uri, dataReaderOptions), IObjectFileUriContext
{

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserves the existing instance/interface API used by dependency-injected callers.")]
    public void Read<TResult>(Func<string, TResult> mapper, Action<IEnumerable<TResult>> reducer)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(reducer);
        reducer(File.ReadLines(Uri.LocalPath).Select(mapper));
    }

    /// <summary>
    ///  read data from a source and maps it to a collection of results using the specified mapper function.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="dataReaderMapper"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Per-row import boundary logs malformed row failures and continues existing import behavior.")]
    public async Task<ICollection<TResult>> ReadAsync<TResult>(Func<string, TResult> dataReaderMapper)
    {
        ArgumentNullException.ThrowIfNull(dataReaderMapper);
        ICollection<TResult> resultSet = [];
        var stringReader = new FileStringReader(Uri);
        await foreach (var line in stringReader.ReadLinesAsync().ConfigureAwait(false))
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
    /// <param name="dataReaderMapper">A function that maps ordinal data from an <see cref="IObjectDataRecord"/> to an instance of <typeparamref
    /// name="TResult"/>. This function is invoked for each record in the data source.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of <typeparamref
    /// name="TResult"/>  objects representing the mapped data.</returns>
    /// <exception cref="NotImplementedException">Thrown if the <see cref="DataReaderOptions.DataReaderType"/> is not supported.</exception>
    public async Task<ICollection<TResult>> ReadAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper)
    {
        return DataReaderOptions.DataReaderType switch
        {
            DataReaderType.Csv => await (GetCsvDataAsync()).ConfigureAwait(false),
            DataReaderType.JSON => await (GetJsonDataAsync()).ConfigureAwait(false),
            _ => throw new NotSupportedException()
        };

        async ValueTask<ICollection<TResult>> GetCsvDataAsync()
        {
            var stringReader = new FileStringReader(Uri);
            using var dataReader = await CsvDataReader<TResult>.CreateAsync(stringReader).ConfigureAwait(false);
            var resultSet = ReadAll(dataReader);
            return resultSet;
        }

        async ValueTask<ICollection<TResult>> GetJsonDataAsync()
        {
            var stringReader = new HttpStringReader(Uri);
            using var dataReader = await JsonDataReader<TResult>.CreateAsync(stringReader).ConfigureAwait(false);
            var resultSet = ReadAll(dataReader);
            return resultSet;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Per-row import boundary logs malformed row failures and continues existing import behavior.")]
        ICollection<TResult> ReadAll(IDataReader dataReader)
        {
            var resultSet = new List<TResult>();
            var record = new AdoNetDataRecord().SetReader(dataReader);
            while (dataReader.Read())
            {
                try
                {

                    var result = dataReaderMapper(record);
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
