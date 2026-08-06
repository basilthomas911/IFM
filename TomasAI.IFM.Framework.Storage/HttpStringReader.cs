namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Provides functionality to read string data from an HTTP or HTTPS URI.
/// </summary>
/// <remarks>This class is designed to fetch and read string content from a specified HTTP or HTTPS URI.  It
/// supports asynchronous operations for reading the entire content of the source.  Note that the <see
/// cref="ReadLinesAsync"/> method is not supported for this implementation.</remarks>
public class HttpStringReader : IStringReader
{
    static readonly HttpClient Client = new();
    readonly Uri _dataSourceUri;

    public HttpStringReader(Uri dataSourceUri)
    {
        if (dataSourceUri == null)
            throw new ArgumentNullException("dataSourceUri", "StringReader constructor parameter is null");
        if (dataSourceUri.Scheme != Uri.UriSchemeHttp && dataSourceUri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("The URI scheme must be 'http' or 'https'.", nameof(dataSourceUri));
        _dataSourceUri = dataSourceUri;
    }

    /// <summary>
    /// Asynchronously reads all lines from the source.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains an  IEnumerable{T} of strings, where
    /// each string represents a line of text.</returns>
    /// <exception cref="NotSupportedException">Always thrown. This method is not supported for HttpStringReader. Use ReadToEndAsync instead.</exception>
    public async Task<IEnumerable<string>> ReadLinesAsync()
    {
        var content = await ReadToEndAsync().ConfigureAwait(false);
        return content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }

    public async Task<string> ReadToEndAsync()
        => await ReadToEndAsync(CancellationToken.None).ConfigureAwait(false);

    public async Task<string> ReadToEndAsync(CancellationToken cancellationToken)
        => await Client.GetStringAsync(_dataSourceUri, cancellationToken).ConfigureAwait(false);

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

    async IAsyncEnumerable<string> IStringReader.ReadLinesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var content = await ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        foreach (var line in content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return line;
        }
    }
}


