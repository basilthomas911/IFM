using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.Storage.Azure
{
    public interface IAzureStorageFile
    {
        string Name { get; }
        string Container { get; }
        string BackupType { get; }
        string Source { get; }
        string Destination { get; }
    }
}
