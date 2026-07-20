using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public class WebConnectionSettings : IWebConnectionSettings
    {
        private Dictionary<string, IWebConnectionSetting> _connSettings;

        /// <summary>
        /// create connection settings 
        /// </summary>
        public WebConnectionSettings()
        {
            _connSettings = new Dictionary<string, IWebConnectionSetting>();
        }

        /// <summary>
        /// return db connection setting by connection name
        /// </summary>
        /// <param name="connectionName"></param>
        /// <returns></returns>
        public IWebConnectionSetting this[string connectionName]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(connectionName))
                    throw new ArgumentNullException("connectionName", "WebConnectionSettings.this[]: 'connectionName' parameter is empty");
                return _connSettings.ContainsKey(connectionName)
                    ? _connSettings[connectionName]
                    : null;
            }
        }

        public int Count => _connSettings.Count;

        public IWebConnectionSettings Add(string connectionName, string baseUri)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                throw new ArgumentNullException("connectionName", "WebConnectionSettings.Add: 'connectionName' parameter is empty");
            if (_connSettings.ContainsKey(connectionName))
                throw new InvalidOperationException($"WebConnectionSettings.Add: connection settings already exists for: '{connectionName}");
            _connSettings.Add(connectionName, new WebConnectionSetting(connectionName, baseUri));
            return this;
        }
    }
}
