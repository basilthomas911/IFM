using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage;

public class DbContextPool<TRepo>(IDbContextFactory dbFactory)
    : IDbContextPool<TRepo> where TRepo : IObjectRepository
{
    static readonly ConcurrentQueue<IObjectRepository<TRepo>> _pool = new();

    /// <summary>
    /// Execute a database function asynchronously using a pooled database context.
    /// </summary>
    /// <param name="dbFunc"></param>
    /// <returns></returns>
    public async Task ExecuteAsync(Func<IObjectRepository<TRepo>, Task>? dbFunc)  
    {
        var db = default(IObjectRepository<TRepo>);
        try
        {
            if (!_pool.TryDequeue(out db) || db == null)
                db = dbFactory.Get<TRepo>();
            await (dbFunc?.Invoke(db))!;
        }
        finally
        {
            if (db is not null)
                _pool.Enqueue(db);
        }
    }

    public Task<ICollection<TResult>> GetAsync< TResult>(Func<IObjectRepository<TRepo>, Task<ICollection<TResult>>> dbFunc)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Asynchronously retrieves a result from the database using a function that returns a TResult.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="dbFunc"></param>
    /// <returns></returns>
    public async Task<TResult?> GetAsync< TResult>(Func<IObjectRepository<TRepo>, Task<TResult?>> dbFunc)
        where TResult : class
    {
        var db = default(IObjectRepository<TRepo>);
        try
        {
            if (!_pool.TryDequeue(out db) || db == null)
                db = dbFactory.Get<TRepo>();
            return await(dbFunc?.Invoke(db))!;
        }
        finally
        {
            if (db is not null)
                _pool.Enqueue(db);
        }
    }

    public Task<TResult> GetScalarAsync< TResult>(Func<IObjectRepository<TRepo>, Task<TResult>> dbFunc)
        where TResult : struct
    {
        throw new NotImplementedException();
    }
}
