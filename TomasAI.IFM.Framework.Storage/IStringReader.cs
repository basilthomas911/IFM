namespace TomasAI.IFM.Framework.Storage;

public interface IStringReader
{
    Task<string> ReadToEndAsync();
    IAsyncEnumerable<string> ReadLinesAsync();
}
