using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Provides a base implementation for an object URI context, managing parameter values for URI-based data access.
/// </summary>
public abstract class ObjectUriContext(
    string commandName,
    Uri uri,
    IDataReaderOptions dataReaderOptions) : IObjectUriContext
{
    readonly List<object> _parameterValues = [];

    /// <summary>
    /// Gets the list of parameter values for the URI context.
    /// </summary>
    public List<object> ParameterValues => _parameterValues;

    /// <summary>
    /// Gets the URI of the file.
    /// </summary>
    public Uri Uri { get; } = IsArgumentNull.Set(uri);

    /// <summary>
    /// Gets the options for the data reader.
    /// </summary>
    public IDataReaderOptions DataReaderOptions { get; } = IsArgumentNull.Set(dataReaderOptions);

    /// <summary>
    /// Gets the globally identifiable name of the URI operation.
    /// </summary>
    public string CommandName { get; } = IsArgumentNull.Set(commandName);

    /// <summary>
    /// Gets the command name and URI formatted for diagnostic logging.
    /// </summary>
    public string CommandLogText =>
        $"command name: {CommandName}{Environment.NewLine}{Uri}";

    /// <summary>
    /// Sets a single parameter value for the URI context, clearing any existing parameters.
    /// </summary>
    /// <typeparam name="TParam">The type of the parameter value.</typeparam>
    /// <param name="parameterValue">The parameter value to set.</param>
    /// <returns>The current instance of the <see cref="IObjectUriContext"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="parameterValue"/> is <see langword="null"/>.</exception>
    public IObjectUriContext SetParameters<TParam>(TParam parameterValue)
    {
        _parameterValues.Clear();
        if (parameterValue is null)
        {
            throw new ArgumentNullException(nameof(parameterValue));
        }
        _parameterValues.Add(parameterValue);
        return this;
    }

    /// <summary>
    /// Sets a collection of parameter values for the URI context, clearing any existing parameters.
    /// </summary>
    /// <typeparam name="TParam">The type of the parameter values.</typeparam>
    /// <param name="parameterValues">The collection of parameter values to set.</param>
    /// <returns>The current instance of the <see cref="IObjectUriContext"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="parameterValues"/> is <see langword="null"/>.</exception>
    public IObjectUriContext SetParameters<TParam>(IEnumerable<TParam> parameterValues)
    {
        _parameterValues.Clear();
        if (parameterValues is null)
        {
            throw new ArgumentNullException(nameof(parameterValues));
        }
        _parameterValues.AddRange(parameterValues.Cast<object>());
        return this;
    }
}
