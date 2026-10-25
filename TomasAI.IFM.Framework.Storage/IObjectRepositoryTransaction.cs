using TomasAI.IFM.Shared.Storage;
namespace TomasAI.IFM.Framework.Storage;
    public interface IObjectRepositoryTransaction<TRepo> : IObjectRepositoryTransaction where TRepo : IObjectRepository
    {
        /// <summary>
        /// start database transaction
        /// </summary>
        /// <param name="db">database to include in transaction</param>
        /// <returns></returns>
        IObjectRepositoryTransaction<TRepo> BeginTransaction(ObjectDataRepository<TRepo> db);

        /// <summary>
        /// create database coommand
        /// </summary>
        /// <returns></returns>
        object? CreateCommand();
    }
