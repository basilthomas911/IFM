using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.Storage.Azure
{
    public interface IAzureStorageOptions
    {
        string ConnectionString { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2227", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
        ICollection<AzureStorageFile> BackupFiles { get; set; }

        IAzureStorageFile? GetStorageFile(string name, string backupType);
    }
}
