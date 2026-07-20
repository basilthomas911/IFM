using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.Storage.Azure
{
    public class AzureStorageFile : IAzureStorageFile
    {
        public string Name {get;set;} = string.Empty;

        public string Container { get; set; } = string.Empty;

        public string BackupType { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string Destination { get; set; } = string.Empty;
    }
}
