using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Azure.Storage.Blobs;
using TomasAI.IFM.Shared.SystemAdmin;

namespace TomasAI.IFM.Framework.Storage.Azure
{
    public class AzureStorage : IAzureStorage
    {
        readonly IAzureStorageOptions _options;
       
        public AzureStorage(IAzureStorageOptions options)
        {
            _options = options;
        }

        public async Task UploadFileAsync(string dbName, string backupType, Func<string, Task>? progressFunc = null)
        {
            try
            {
                // Create a BlobServiceClient object which will be used to create a container client...
                var blobServiceClient = new BlobServiceClient(_options.ConnectionString);

                // Create the container and return a container client object..
                var storageFile = _options.GetStorageFile(dbName, backupType);
                var blobContainer = blobServiceClient.GetBlobContainerClient(storageFile.Container);
                if (blobContainer is null)
                    throw new InvalidOperationException($"AzureStorage.UploadFileAsync: Container '{storageFile.Container ?? string.Empty}' does not exist");

                // Get a reference to a blob...
                var blobClient = blobContainer.GetBlobClient(storageFile.Destination);

                // Upload data from backup file...
                if (progressFunc is not null)
                    await progressFunc.Invoke($"Uploading {storageFile.Source} to {storageFile.Container}\\{storageFile.Destination}...");
                using (var stream = new FileStream(storageFile.Source, FileMode.Open, FileAccess.Read))
                {
                    await blobClient.UploadAsync(stream, true);
                }
                if (progressFunc is not null)
                    await progressFunc.Invoke($"Upload of {storageFile.Source} to {storageFile.Container}\\{storageFile.Destination} completed");
            }
            catch (Exception ex)
            {
                await progressFunc.Invoke($"Unable to upload {dbName} due to {ex.Message}");
            }
        }

        public IAzureStorageFile GetStorageFile(string name, DatabaseBackupType backupType) => _options.GetStorageFile(name, $"{backupType}".ToLower());

    }
}
