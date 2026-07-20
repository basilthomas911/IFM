namespace TomasAI.IFM.Framework.Storage;

public interface IObjectUriContext
{
    IObjectUriContext SetParameters<TParam>(TParam parameterValue);
    IObjectUriContext SetParameters<TParam>(IEnumerable<TParam> parameterValues);

    /// <summary>
    /// Gets the URI of the file.
    /// </summary>
    Uri Uri { get; }

    /// <summary>
    /// Gets the options for the data reader.
    /// </summary>
    IDataReaderOptions DataReaderOptions { get; }

}
