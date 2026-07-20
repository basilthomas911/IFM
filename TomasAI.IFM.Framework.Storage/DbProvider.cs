using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

namespace TomasAI.IFM.Framework.Storage
{
    public abstract class DbProvider : IObjectCreateProvider
    {
        readonly  IObjectRepository _repo;
        List<CommandType>? _queuedCommandTypes;
        readonly ILogger<DbProvider> _logger;

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
        {
            (_queuedCommandTypes ??= []).Add(CommandType.StoredProcedure);
            return new ObjectDataStoredProcedureContext(_repo, _logger,  storedProcName);
        }

        /// <summary>
        /// create command text context
        /// </summary>
        /// <param name="cmdText"></param>
        /// <returns></returns>
        public IObjectRepositoryContext CreateCommandTextContext(string cmdText)
        {
            (_queuedCommandTypes ??= []).Add(CommandType.Text);
            return new ObjectDataCommandTextContext(_repo, _logger, cmdText);
        }

        /// <summary>
        /// create queued commands context
        /// </summary>
        /// <param name="queuedCommands"></param>
        /// <returns></returns>
        public virtual IObjectRepositoryContext CreateQueuedCommandsContext()
        {
            if (_queuedCommandTypes is null || _queuedCommandTypes.Count == 0)
                throw new ArgumentException("DbProvider.CreateQueuedCommandsContext: no queued commands");
            IObjectRepositoryContext? repoContext = _queuedCommandTypes switch
            {
                _ when _queuedCommandTypes.All( e => e == CommandType.Text) => new ObjectDataCommandTextContext(_repo, _logger),
                _ when _queuedCommandTypes.All(e => e ==  CommandType.StoredProcedure) => new ObjectDataStoredProcedureContext(_repo, _logger),
                _ => default
            };
            _queuedCommandTypes.Clear();
            if (repoContext is  null)
                throw new ArgumentException("DbProvider.CreateQueuedCommandsContext: all queued commands must use same context type");
            return repoContext;
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
