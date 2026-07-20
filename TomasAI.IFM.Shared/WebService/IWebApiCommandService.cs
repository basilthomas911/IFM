using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public interface IWebApiCommandService
    {
        void SetBaseUri(Uri baseUri);
        Task<TOut> PostAsync<TIn, TOut>(string postUri, TIn postBody);
        TOut Post<TIn, TOut>(string postUri, TIn postBody);
        Task<TOut> PutAsync<TIn, TOut>(string putUri, TIn putBody);
        Task<TOut> DeleteAsync<TOut>(string deleteUri);
    }
}
