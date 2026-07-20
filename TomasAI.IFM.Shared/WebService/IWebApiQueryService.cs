using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public interface IWebApiQueryService
    {
        Task<TOut> GetAsync<TOut>(string getUri);
        TOut Get<TOut>(string getUri);
    }
}
