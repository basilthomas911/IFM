namespace TomasAI.IFM.Framework.Storage;

public interface IStringReader
{
    Task<string> ReadToEndAsync();
    Task<string> ReadToEndAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<string> ReadLinesAsync();
    IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken);
}
