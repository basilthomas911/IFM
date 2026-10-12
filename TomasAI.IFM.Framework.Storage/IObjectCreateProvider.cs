using System.Data;

namespace TomasAI.IFM.Framework.Storage;

public interface IObjectCreateProvider
{
    IObjectRepositoryConnection CreateConnection();
    IObjectRepositoryParameter CreateParameter();
    IObjectRepositoryContext CreateStoredProcedureContext(string storedProcName);
    IObjectRepositoryContext CreateCommandTextContext(string commandName, string commandText);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1133", Justification = "Obsolete adapter remains until provider migration completes; callers are directed to validated queue metadata.")]
    [Obsolete("Use CreateQueuedCommandsContext(IReadOnlyCollection<object>) so queue metadata can be validated.")]
    IObjectRepositoryContext CreateQueuedCommandsContext();
    IObjectRepositoryContext CreateQueuedCommandsContext(IReadOnlyCollection<object> queuedCommands)
#pragma warning disable CS0618 // Compatibility fallback for existing IObjectCreateProvider implementations.
        => CreateQueuedCommandsContext();
#pragma warning restore CS0618
    IObjectRepositoryTransaction<TRepo>? CreateTransaction<TRepo>() where TRepo : IObjectRepository;
    IObjectBulkCopyContext CreateBulkCopyContext(DataTable bulkCopyDataTable);
    IObjectDataReaderContext CreateDataReaderContext(IDataReaderOptions dataReaderOptions);
    IObjectUriContext CreateFileUriContext(string commandName, Uri uriObject, IDataReaderOptions dataReaderOptions);
    IObjectUriContext CreateHttpUriContext(string commandName, Uri uriObject, IDataReaderOptions dataReaderOptions);
}
