using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Storage
{
    public class StorageUrlSetting : IStorageUrlSetting
    {
        private readonly string _name;
        private readonly string _storageUrl;

        public string Name => _name;
        public string StorageUrl => _storageUrl;

        /// <summary>
        /// create spreading
        /// </summary>
        /// <param name="name"></param>
        /// <param name="connectionString"></param>
        /// <param name="providerName"></param>
        public StorageUrlSetting(string name, Uri storageUrl)
            : this(name, (storageUrl ?? throw new ArgumentNullException(nameof(storageUrl))).OriginalString) { }

        public StorageUrlSetting(string name, string storageUrl)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name), "StorageUrlSetting: constructor parameter 'name' is empty");
            if (string.IsNullOrWhiteSpace(storageUrl))
                throw new ArgumentNullException(nameof(storageUrl), "StorageUrlSetting: constructor parameter 'storageUrl' is empty");
            _name = name;
            _storageUrl = storageUrl;
        }

    }
}
