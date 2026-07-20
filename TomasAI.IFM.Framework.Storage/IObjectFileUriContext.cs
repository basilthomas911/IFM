namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Defines the contract for a file URI context, which provides access to a URI and data reader options.
/// </summary>
public interface IObjectFileUriContext 
{
    Task<ICollection<TResult>> ReadAsync<TResult>(Func<IObjectMapReader<TResult>, TResult> dataReaderMapper);
}
