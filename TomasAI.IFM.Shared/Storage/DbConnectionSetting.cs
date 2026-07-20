using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Storage
{
    public class DbConnectionSetting : IDbConnectionSetting
    {
        private string _name;
        private string _connectionString;
        private string _providerName;

        /// <summary>
        /// create spreading
        /// </summary>
        /// <param name="name"></param>
        /// <param name="connectionString"></param>
        /// <param name="providerName"></param>
        public DbConnectionSetting(string name, string connectionString, string providerName)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name", "DbConnectionSetting: constructor parameter 'name' is empty");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException("connectionString", "DbConnectionSetting: constructor parameter 'connectionString' is empty");
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentNullException("providerName", "DbConnectionSetting: constructor parameter 'providerName' is empty");
            _name = name;
            _connectionString = connectionString;
            _providerName = providerName;
        }

        public string Name => _name;
        public string ConnectionString => _connectionString;
        public string ProviderName => _providerName;


    }
}
