using System.Data;

namespace TomasAI.IFM.Framework.Storage;

public interface IObjectCreateProvider
{
    IObjectRepositoryConnection CreateConnection();
    IObjectRepositoryParameter CreateParameter();
    IObjectRepositoryContext CreateStoredProcedureContext(string storedProcName);
    IObjectRepositoryContext CreateCommandTextContext(string commandText);
    IObjectRepositoryContext CreateQueuedCommandsContext();
    IObjectRepositoryTransaction<TRepo>? CreateTransaction<TRepo>() where TRepo : IObjectRepository;
    IObjectBulkCopyContext CreateBulkCopyContext(DataTable bulkCopyDataTable);
    IObjectDataReaderContext CreateDataReaderContext(IDataReaderOptions dataReaderOptions);
    IObjectUriContext CreateFileUriContext(Uri uriObject, IDataReaderOptions dataReaderOptions);
    IObjectUriContext CreateHttpUriContext(Uri uriObject, IDataReaderOptions dataReaderOptions);
}
