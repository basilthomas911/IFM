using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage;

public interface IDbContextPool<TRepo> where TRepo : IObjectRepository
{
    Task ExecuteAsync(Func<IObjectRepository<TRepo>, Task>? dbFunc);
    Task<ICollection<TResult>> GetAsync<TResult>(Func<IObjectRepository<TRepo>, Task<ICollection<TResult>>> dbFunc);
    Task<TResult?> GetAsync< TResult>(Func<IObjectRepository<TRepo>, Task<TResult?>> dbFunc)  where TResult : class;
    Task<TResult> GetScalarAsync<TResult>(Func<IObjectRepository<TRepo>, Task<TResult>> dbFunc)  where TResult : struct;
}
