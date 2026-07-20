namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Provides functionality to read string data from an HTTP or HTTPS URI.
/// </summary>
/// <remarks>This class is designed to fetch and read string content from a specified HTTP or HTTPS URI.  It
/// supports asynchronous operations for reading the entire content of the source.  Note that the <see
/// cref="ReadLinesAsync"/> method is not supported for this implementation.</remarks>
public class HttpStringReader : IStringReader
{
    Func<Task<string>> _readString;

    public HttpStringReader(Uri dataSourceUri)
    {
        if (dataSourceUri == null)
            throw new ArgumentNullException("dataSourceUri", "StringReader constructor parameter is null");
        _readString = async () =>
        {
            if (dataSourceUri.Scheme != Uri.UriSchemeHttp && dataSourceUri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("The URI scheme must be 'http' or 'https'.", nameof(dataSourceUri));
            using var httpClient = new HttpClient();
            return await httpClient.GetStringAsync(dataSourceUri);
        };
    }

    /// <summary>
    /// Asynchronously reads all lines from the source.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains an  IEnumerable{T} of strings, where
    /// each string represents a line of text.</returns>
    /// <exception cref="NotSupportedException">Always thrown. This method is not supported for HttpStringReader. Use ReadToEndAsync instead.</exception>
    public async Task<IEnumerable<string>> ReadLinesAsync()
    {
        var content = await _readString();
        return content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }

    public async Task<string> ReadToEndAsync()
        => await _readString();

    /// <summary>
    /// Asynchronously reads lines of text from the underlying source.
    /// </summary>
    /// <remarks>This method returns an asynchronous stream of strings, where each string represents a single
    /// line of text. The caller can enumerate the lines using an asynchronous foreach loop.</remarks>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of strings, where each string is a line of text from the source.</returns>
    /// <exception cref="NotImplementedException">This method is not yet implemented.</exception>
    async IAsyncEnumerable<string> IStringReader.ReadLinesAsync()
    {
        foreach(var line in await ReadLinesAsync())
        {
            yield return line;
        }
    }
}


