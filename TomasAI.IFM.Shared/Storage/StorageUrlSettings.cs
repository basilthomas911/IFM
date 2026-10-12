using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Storage
{
    public class StorageUrlSettings : IStorageUrlSettings
    {
        private readonly Dictionary<string, IStorageUrlSetting> _storageUrlSettings;

        public StorageUrlSettings()
        {
            _storageUrlSettings = new Dictionary<string, IStorageUrlSetting>(StringComparer.Ordinal);
        }

        public IStorageUrlSetting this[string storageName]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(storageName))
                    throw new ArgumentNullException(nameof(storageName), "StorageUrlSettings.this[]: 'storageName' parameter is empty");
                return _storageUrlSettings.TryGetValue(storageName, out var setting) ? setting : null!;
            }
        }

        public int Count => _storageUrlSettings.Count;

        public IStorageUrlSettings Add(string storageName, Uri storageUrl)
        {
            ArgumentNullException.ThrowIfNull(storageUrl);
            return Add(storageName, storageUrl.OriginalString);
        }

        public IStorageUrlSettings Add(string storageName, string storageUrl)
        {
            if (string.IsNullOrWhiteSpace(storageName))
                throw new ArgumentNullException(nameof(storageName), "StorageUrlSettings.Add: 'storageName' parameter is empty");
            if (_storageUrlSettings.ContainsKey(storageName))
                throw new InvalidOperationException($"StorageUrlSettings.Add: storage url settings already exists for: '{storageName}");
            _storageUrlSettings.Add(storageName, new StorageUrlSetting(storageName, storageUrl));
            return this;
        }
    }
}
