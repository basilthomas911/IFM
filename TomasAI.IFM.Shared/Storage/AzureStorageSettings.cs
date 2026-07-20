using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Storage
{
    public class AzureStorageSettings : IAzureStorageSettings
    {
        private readonly string _containerName;
        private readonly string _fullSourceFileName;
        private readonly string _fullDestinationFileName;
        private readonly string _diffSourceFileName;
        private readonly string _diffDestinationFileName;

        public AzureStorageSettings(
            string containerName,
            string fullSourceFileName,
            string fullDestinationFileName,
            string diffSourceFileName,
            string diffDestinationFileName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
                throw new ArgumentNullException("containerName", "AzureStorageSettings: constructor parameter 'containerName' is empty");
            if (string.IsNullOrWhiteSpace(fullSourceFileName))
                throw new ArgumentNullException("fullSourceFileName", "AzureStorageSettings: constructor parameter 'fullSourceFileName' is empty");
            if (string.IsNullOrWhiteSpace(fullDestinationFileName))
                throw new ArgumentNullException("fullDestinationFileName", "AzureStorageSettings: constructor parameter 'fullDestinationFileName' is empty");
            if (string.IsNullOrWhiteSpace(diffSourceFileName))
                throw new ArgumentNullException("diffSourceFileName", "AzureStorageSettings: constructor parameter 'diffSourceFileName' is empty");
            if (string.IsNullOrWhiteSpace(diffDestinationFileName))
                throw new ArgumentNullException("diffDestinationFileName", "AzureStorageSettings: constructor parameter 'diffDestinationFileName' is empty");
            _containerName = containerName;
            _fullSourceFileName = fullSourceFileName;
            _fullDestinationFileName = fullDestinationFileName;
            _diffSourceFileName = diffSourceFileName;
            _diffDestinationFileName = diffDestinationFileName;
        }

        public string ContainerName => _containerName;
        public string FullSourceFileName => _fullSourceFileName;
        public string FullDestinationFileName => _fullDestinationFileName;
        public string DiffSourceFileName => _diffSourceFileName;
        public string DiffDestinationFileName => _diffDestinationFileName;
    }
}
