using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.Storage.Azure
{
    public interface IAzureStorageOptions
    {
        string ConnectionString { get; set; }
        ICollection<AzureStorageFile> BackupFiles { get; set; }

        IAzureStorageFile? GetStorageFile(string name, string backupType);
    }
}
