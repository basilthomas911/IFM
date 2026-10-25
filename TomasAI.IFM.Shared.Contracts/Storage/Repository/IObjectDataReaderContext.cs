namespace TomasAI.IFM.Shared.Storage;

public interface IObjectDataReaderContext
{
    Task<ICollection<TResult>> ReadAsync<TResult>(Func<IObjectDataRecord, TResult> mapper);
    Task<ICollection<TResult>> ReadAsync<TResult>(
        Func<IObjectDataRecord, TResult> mapper,
        CancellationToken cancellationToken);
}
