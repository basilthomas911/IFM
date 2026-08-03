namespace TomasAI.IFM.Framework.Storage;

public interface IObjectDataReaderContext
{
    Task<ICollection<TResult>> ReadAsync<TResult>(Func<IObjectDataRecord, TResult> mapper);
}
