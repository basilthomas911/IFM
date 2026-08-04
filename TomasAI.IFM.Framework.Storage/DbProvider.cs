using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

namespace TomasAI.IFM.Framework.Storage
{
    public abstract class DbProvider : IObjectCreateProvider
    {
        readonly  IObjectRepository _repo;
        readonly ILogger<DbProvider> _logger;
        readonly Lazy<object> _connectionIdentity;

        /// <summary>
        /// create objects that are requested by repository object
        /// </summary>
        /// <param name="repo"></param>
        public DbProvider(IObjectRepository repo, ILogger<DbProvider> logger)
        {
            if (repo == null)
                throw new ArgumentException("DbCreateProvider: repository parameter is empty");
            _repo = repo;
            _logger = logger;
            // ObjectDataRepository creates this provider before assigning its immutable
            // connection settings, so defer the digest until the first queue validation.
            _connectionIdentity = new Lazy<object>(
                () => RepositoryConnectionIdentity.Get(repo),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

          /// <summary>
        /// create ado.net IDbConnection object
        /// </summary>
        /// <returns></returns>
        public  IObjectRepositoryConnection CreateConnection()
            => ObjectDataRepositoryConnection.Create(_repo.ProviderName);

        /// <summary>
        /// create ado.net IDbDataParameter object
        /// </summary>
        /// <returns></returns>
        public IObjectRepositoryParameter CreateParameter()
            => ObjectDataRepositoryParameter.Create(_repo.ProviderName);

        /// <summary>
        /// create stored procedure context
        /// </summary>
        /// <param name="storedProcName"></param>
        /// <returns></returns>
        public IObjectRepositoryContext CreateStoredProcedureContext(string storedProcName)
            => new ObjectDataStoredProcedureContext(_repo, _logger, storedProcName);

        /// <summary>
        /// create command text context
        /// </summary>
        /// <param name="cmdText"></param>
        /// <returns></returns>
        public IObjectRepositoryContext CreateCommandTextContext(string cmdText)
            => new ObjectDataCommandTextContext(_repo, _logger, cmdText);

        /// <summary>
        /// create queued commands context
        /// </summary>
        /// <param name="queuedCommands"></param>
        /// <returns></returns>
        [Obsolete("Use CreateQueuedCommandsContext(IReadOnlyCollection<object>) so queue metadata can be validated.")]
        public virtual IObjectRepositoryContext CreateQueuedCommandsContext()
            => new ObjectDataCommandTextContext(_repo, _logger);

        /// <summary>
        /// Creates an execution context from metadata carried by the supplied queue.
        /// Independent callers can therefore build and execute queues concurrently
        /// without sharing or clearing repository-global bookkeeping.
        /// </summary>
        public virtual IObjectRepositoryContext CreateQueuedCommandsContext(
            IReadOnlyCollection<object> queuedCommands)
        {
            ArgumentNullException.ThrowIfNull(queuedCommands);
            if (queuedCommands.Count == 0)
                throw new ArgumentException("DbProvider.CreateQueuedCommandsContext: no queued commands");

            CommandType? commandType = null;
            foreach (var queuedCommand in queuedCommands)
            {
                if (queuedCommand is not IObjectDataQueuedCommandMetadata metadata)
                {
                    throw new ArgumentException(
                        "DbProvider.CreateQueuedCommandsContext: unsupported queued command type");
                }
                if (!string.IsNullOrEmpty(metadata.ProviderName) &&
                    !string.Equals(metadata.ProviderName, _repo.ProviderName, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "DbProvider.CreateQueuedCommandsContext: all queued commands must use the repository provider");
                }
                if (metadata.ConnectionIdentity is not null &&
                    !metadata.ConnectionIdentity.Equals(_connectionIdentity.Value))
                {
                    throw new ArgumentException(
                        "DbProvider.CreateQueuedCommandsContext: all queued commands must use the repository connection");
                }
                if (commandType.HasValue && commandType.Value != metadata.CommandType)
                {
                    throw new ArgumentException(
                        "DbProvider.CreateQueuedCommandsContext: all queued commands must use same context type");
                }
                commandType = metadata.CommandType;
            }

            return commandType switch
            {
                CommandType.Text => new ObjectDataCommandTextContext(_repo, _logger),
                CommandType.StoredProcedure => new ObjectDataStoredProcedureContext(_repo, _logger),
                _ => throw new ArgumentException(
                    "DbProvider.CreateQueuedCommandsContext: unsupported queued command context type")
            };
        }

        /// <summary>
        /// create command text context
        /// </summary>
        /// <param name="repo"></param>
        /// <returns></returns>
        public IObjectBulkCopyContext CreateBulkCopyContext(DataTable bulkCopyDataTable) 
            => new ObjectDataBulkCopyContext(_repo, bulkCopyDataTable);

        public IObjectDataReaderContext CreateDataReaderContext(IDataReaderOptions dataReaderOptions)
            => new ObjectDataReaderContext(_repo, dataReaderOptions);

        public IObjectUriContext CreateFileUriContext(Uri uriObject, IDataReaderOptions dataReaderOptions)
            =>  new ObjectFileUriContext(uriObject, dataReaderOptions);

        public IObjectUriContext CreateHttpUriContext(Uri uriObject, IDataReaderOptions dataReaderOptions)
            => default!; // new ObjectDataReaderContext(_repo, dataReaderOptions, fileUri);

        public IObjectRepositoryTransaction<TRepo>? CreateTransaction<TRepo>() where TRepo : IObjectRepository
            => ObjectDataRepositoryTransaction.Create<TRepo>(_repo.ProviderName);
    }

}
