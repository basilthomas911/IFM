using System;
using System.Collections.Generic;
using System.Text;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace TomasAI.IFM.Framework.Storage
{
    public interface IObjectRepositoryTransaction
    {
         /// <summary>
        /// commit database transaction
        /// </summary>
        void Commit();

        /// <summary>
        /// rollback database transaction
        /// </summary>
        void Rollback();
    }

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
}
