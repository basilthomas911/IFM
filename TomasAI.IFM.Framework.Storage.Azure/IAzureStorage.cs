using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.SystemAdmin;

namespace TomasAI.IFM.Framework.Storage.Azure
{
    public interface IAzureStorage
    {
        Task UploadFileAsync(string dbName, string backupType, Func<string,Task>? progressFunc = null);
        IAzureStorageFile GetStorageFile(string name, DatabaseBackupType backupType);
    }


}
