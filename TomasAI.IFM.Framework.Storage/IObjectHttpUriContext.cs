namespace TomasAI.IFM.Framework.Storage;

public interface IObjectHttpUriContext: IObjectUriContext
{
    public Task<ICollection<TResult>> GetAsync<TResult>();
    public Task<TResult> GetSingleAsync<TResult>();
}
