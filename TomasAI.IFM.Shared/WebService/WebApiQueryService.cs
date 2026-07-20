using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public class WebApiQueryService : WebApiService, IWebApiQueryService
    {
        public WebApiQueryService(IWebApiSettings webApiSettings)
            :base(webApiSettings.WebApiQueryUri)
        {
        }
    }
}
