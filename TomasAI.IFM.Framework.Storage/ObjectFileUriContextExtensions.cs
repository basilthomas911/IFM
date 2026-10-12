using System.Data;
using System.IO;
using TomasAI.IFM.Framework.Storage.Csv;
using TomasAI.IFM.Framework.Storage.Json;

namespace TomasAI.IFM.Framework.Storage;

public static class ObjectFileUriContextExtensions
{
    /// <summary>
    /// Asynchronously reads data from the specified <see cref="IObjectUriContext"/> and maps it to a collection of
    /// results.
    /// </summary>
    /// <typeparam name="TResult">The type of the objects to be read and mapped.</typeparam>
    /// <param name="uriCtx">The context representing the source of the data to be read. Must be an instance of <see
    /// cref="ObjectFileUriContext"/>.</param>
    /// <param name="dataReaderMapper">A function that maps an <see cref="IObjectDataRecord"/> to an instance of <typeparamref
    /// name="TResult"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of <typeparamref
    /// name="TResult"/> objects mapped from the data source.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="uriCtx"/> is not an instance of <see cref="ObjectFileUriContext"/>.</exception>
    public static async Task<ICollection<TResult>> ReadAsync<TResult>(this IObjectUriContext uriCtx, Func<IObjectDataRecord, TResult> dataReaderMapper)
    {
        if (uriCtx is not ObjectFileUriContext objectFileUriContext)
        {
            throw new ArgumentException("The provided context is not an instance of ObjectFileUriContext.", nameof(uriCtx));
        }
        return await (objectFileUriContext.ReadAsync(dataReaderMapper)).ConfigureAwait(false);
    }

    public static async Task<ICollection<TResult>> ReadAsync<TResult>(this IObjectUriContext uriCtx, Func<string, TResult> dataReaderMapper)
    {
        if (uriCtx is not ObjectFileUriContext objectFileUriContext)
        {
            throw new ArgumentException("The provided context is not an instance of ObjectFileUriContext.", nameof(uriCtx));
        }
        return await (objectFileUriContext.ReadAsync(dataReaderMapper)).ConfigureAwait(false);
    }

    public static async Task ReadAsync<TResult>(this IObjectUriContext uriCtx, Func<string, int, TResult> mapper, Func<IEnumerable<TResult>, Task> reducer)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(reducer);
        ArgumentNullException.ThrowIfNull(uriCtx);
        if (uriCtx is not ObjectFileUriContext)
        {
            throw new ArgumentException("The provided context is not an instance of ObjectFileUriContext.", nameof(uriCtx));
        }
        var stringReader = new FileStringReader(uriCtx.Uri);
        var resultSet = new List<TResult>();
        await foreach (var row in stringReader.ReadLinesAsync().ConfigureAwait(false))
        {
            var result = mapper(row, 0);
            if (result is not null)
                resultSet.Add(result);
        }
        await reducer(resultSet).ConfigureAwait(false);
    }
}
