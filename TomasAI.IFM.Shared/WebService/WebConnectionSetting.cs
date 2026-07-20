using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public class WebConnectionSetting : IWebConnectionSetting
    {
        private string _name;
        private string _baseUri;

        public string Name => _name;
        public string BaseUri => _baseUri;

        /// <summary>
        /// create web connection setting
        /// </summary>
        /// <param name="name"></param>
        /// <param name="baseUri"></param>
        public WebConnectionSetting(string name, string baseUri)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name", "WebConnectionSetting: constructor parameter 'name' is empty");
            if (string.IsNullOrWhiteSpace(baseUri))
                throw new ArgumentNullException("baseUri", "WebConnectionSetting: constructor parameter 'baseUri' is empty");
            _name = name;
            _baseUri = baseUri;
        }

    }
}
