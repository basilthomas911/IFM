using Npgsql;
using QLNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.Postgres
{
    public class PostgresObjectDataRepositoryTransaction<TRepo> : IObjectRepositoryTransaction<TRepo> where TRepo : IObjectRepository
    {
        const string ClassName = nameof(PostgresObjectDataRepositoryTransaction<TRepo>);

        // following properties available for unit test outside of framework storage assemblies by settings in csproj file
        public ObjectDataRepository<TRepo>? Repository { get; set; }
        public NpgsqlConnection? Connection { get; set; }
        public NpgsqlTransaction? Transaction { get; set; }

        /// <summary>
        /// begin database transaction
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public IObjectRepositoryTransaction<TRepo> BeginTransaction(ObjectDataRepository<TRepo> db) 
        {
            if (Transaction is not null) throw new StorageException($"{ClassName}.BeginTransaction: transaction already started");
            Repository = db ?? throw new ArgumentNullException(nameof(db));
            Connection = db.CreateConnection().As<NpgsqlConnection>(db.ConnectionString);
            Connection.Open();
            Transaction = Connection.BeginTransaction();
            return this;
        }

        /// <summary>
        /// create database command
        /// </summary>
        /// <returns></returns>
        public object CreateCommand()
        {
            if (Transaction is null) throw new StorageException($"{ClassName}.CreateCommand: transaction has not been started");
            if (Connection is null) throw new InvalidOperationException($"{ClassName}.CreateCommand: connection has not been opened");
            var sqlCommand = Connection.CreateCommand();
            sqlCommand.Transaction = Transaction;
            return sqlCommand;
        }

        /// <summary>
        /// commit database transaction
        /// </summary>
        public void Commit()
        {
            if (Transaction is null) throw new InvalidOperationException($"{ClassName}.Commit: transaction has not been started");
            Transaction.Commit();
            Transaction.Dispose();
            Transaction = null;
            Connection?.Close();
            Connection = null;
            Repository?.SetTransactionCompleted();
            Repository = null;
        }

        /// <summary>
        /// rollbalc database transaction
        /// </summary>
        public void Rollback()
        {
            if (Transaction is null) throw new InvalidOperationException($"{ClassName}.Rollback: transaction has not been started");
            Transaction.Rollback();
            Transaction.Dispose();
            Transaction = null;
            Connection?.Close();
            Connection = null;
            Repository?.SetTransactionCompleted();
            Repository = null;
        }



    }
}
