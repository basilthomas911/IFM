using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public class WebApiSettings : IWebApiSettings
    {
        private Dictionary<string, Uri> _uriMap;

        public WebApiSettings()
        {
            _uriMap = new Dictionary<string, Uri>();
        }

        public Uri WebApiCommandUri => _uriMap.ContainsKey("WebApiCommandUri") ? _uriMap["WebApiCommandUri"] : default(Uri);

        public Uri WebApiQueryUri => _uriMap.ContainsKey("WebApiQueryUri") ? _uriMap["WebApiQueryUri"] : default(Uri);

        public int Count => _uriMap.Count;

        public IWebApiSettings Add(string uriName, Uri uriValue)
        {
            if (!_uriMap.ContainsKey(uriName))
                _uriMap.Add(uriName, uriValue);
            return this;
        }
    }
}
