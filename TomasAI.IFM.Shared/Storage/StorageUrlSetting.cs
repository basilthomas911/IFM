using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Storage
{
    public class StorageUrlSetting : IStorageUrlSetting
    {
        private string _name;
        private string _storageUrl;

        public string Name => _name;
        public string StorageUrl => _storageUrl;

        /// <summary>
        /// create spreading
        /// </summary>
        /// <param name="name"></param>
        /// <param name="connectionString"></param>
        /// <param name="providerName"></param>
        public StorageUrlSetting(string name, string storageUrl)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name", "StorageUrlSetting: constructor parameter 'name' is empty");
            if (string.IsNullOrWhiteSpace(storageUrl))
                throw new ArgumentNullException("storageUrl", "StorageUrlSetting: constructor parameter 'storageUrl' is empty");
            _name = name;
            _storageUrl = storageUrl;
        }

    }
}
