using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public class WebApiCommandService : WebApiService, IWebApiCommandService
    {
        public WebApiCommandService(IWebApiSettings webApiSettings)
            :base(webApiSettings.WebApiCommandUri)
        {
        }
    }
}
