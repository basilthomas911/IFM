using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Storage
{
    public class DbConnectionSetting : IDbConnectionSetting
    {
        private readonly string _name;
        private readonly string _connectionString;
        private readonly string _providerName;

        /// <summary>
        /// create spreading
        /// </summary>
        /// <param name="name"></param>
        /// <param name="connectionString"></param>
        /// <param name="providerName"></param>
        public DbConnectionSetting(string name, string connectionString, string providerName)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name), "DbConnectionSetting: constructor parameter 'name' is empty");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString), "DbConnectionSetting: constructor parameter 'connectionString' is empty");
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentNullException(nameof(providerName), "DbConnectionSetting: constructor parameter 'providerName' is empty");
            _name = name;
            _connectionString = connectionString;
            _providerName = providerName;
        }

        public string Name => _name;
        public string ConnectionString => _connectionString;
        public string ProviderName => _providerName;


    }
}
