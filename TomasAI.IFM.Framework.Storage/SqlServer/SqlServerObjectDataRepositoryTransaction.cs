using Microsoft.Data.SqlClient;

namespace TomasAI.IFM.Framework.Storage.SqlServer
{
    public class SqlServerObjectDataRepositoryTransaction<TRepo> : IObjectRepositoryTransaction<TRepo> where TRepo : IObjectRepository
    {
        public SqlServerObjectDataRepositoryTransaction()
        {
        }

        // following properties available for unit test outside of framework storage assemblies by settings in csproj file
        public  ObjectDataRepository<TRepo>? Repository { get;  set; }
        public  SqlConnection? Connection { get;  set; }
        public SqlTransaction? Transaction { get;  set; }

        /// <summary>
        /// begin database transaction
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public IObjectRepositoryTransaction<TRepo> BeginTransaction(ObjectDataRepository<TRepo> db)
        {
            if (Transaction is not null) throw new InvalidOperationException("SqlServerObjectDataRepositoryTransaction.BeginTransaction: transaction already started");
            Repository = db ?? throw new ArgumentNullException(nameof(db));
            Connection = db.CreateConnection().As<SqlConnection>(db.ConnectionString);
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
            if (Transaction is null) throw new InvalidOperationException("SqlServerObjectDataRepositoryTransaction.CreateCommand: transaction has not been started");
            if (Connection is null) throw new InvalidOperationException("SqlServerObjectDataRepositoryTransaction.CreateCommand: connection has not been opened");
            var sqlCommand = Connection.CreateCommand();
            sqlCommand.Transaction = Transaction;
            return sqlCommand;
        }

        /// <summary>
        /// commit database transaction
        /// </summary>
        public void Commit()
        {
            if (Transaction is null) throw new InvalidOperationException("SqlServerObjectDataRepositoryTransaction.Commit: transaction has not been started");
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
            if (Transaction is null) throw new InvalidOperationException("SqlServerObjectDataRepositoryTransaction.Rollback: transaction has not been started");
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
