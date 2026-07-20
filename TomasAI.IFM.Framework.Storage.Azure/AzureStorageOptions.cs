using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TomasAI.IFM.Framework.Storage.Azure
{
    public class AzureStorageOptions : IAzureStorageOptions
    {
        public AzureStorageOptions()
        {
            BackupFiles = new List<AzureStorageFile>(); 
        }

        public string ConnectionString { get; set; } = string.Empty;
        public ICollection<AzureStorageFile> BackupFiles { get; set; }

        public IAzureStorageFile? GetStorageFile(string name, string backupType)
            => BackupFiles.Where(e => e.Name == name && e.BackupType == backupType).FirstOrDefault();
        
    }
}
