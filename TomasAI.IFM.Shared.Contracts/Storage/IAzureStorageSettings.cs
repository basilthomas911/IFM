using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Storage
{
    public interface IAzureStorageSettings
    {
        string ContainerName { get; }
        string FullSourceFileName { get; }
        string FullDestinationFileName { get; }
        string DiffSourceFileName { get; }
        string DiffDestinationFileName { get; }
    }
}
