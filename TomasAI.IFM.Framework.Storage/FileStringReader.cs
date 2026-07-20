
namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Implements the <see cref="IStringReader"/> interface to read the entire content of a file specified by a URI.
/// </summary>
public class FileStringReader : IStringReader
{
    readonly Uri _dataSourceUri;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileStringReader"/> class with the specified file URI.
    /// </summary>
    /// <param name="dataSourceUri">The URI of the file to be read. Must be a valid file URI.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="dataSourceUri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if the URI scheme is not 'file'.</exception>
    public FileStringReader(Uri dataSourceUri)
    {
        ArgumentNullException.ThrowIfNull(dataSourceUri, nameof(dataSourceUri));
        if (!dataSourceUri.IsFile)
            throw new ArgumentException("The URI scheme must be 'file'.", nameof(dataSourceUri));
        _dataSourceUri = dataSourceUri;
    }

    /// <summary>
    /// Asynchronously reads all lines from the file specified by the data source URI.
    /// </summary>
    /// <remarks>This method reads the entire file into memory. For large files, consider using a streaming 
    /// approach to avoid high memory usage.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains an  IEnumerable{T} of strings, where
    /// each string is a line from the file.</returns>
    public async IAsyncEnumerable<string> ReadLinesAsync()
    {
        await foreach (var line in File.ReadLinesAsync(_dataSourceUri.LocalPath))
        {
            yield return line;
        }
    }

    /// <summary>
    /// Asynchronously reads all characters from the current position to the end of the file.
    /// </summary>
    /// <returns>A task that represents the asynchronous read operation. The value of the TResult parameter contains all characters from the current position to the end of the file.</returns>
    public async Task<string> ReadToEndAsync()
        => await File.ReadAllTextAsync(_dataSourceUri.LocalPath);
    
}
