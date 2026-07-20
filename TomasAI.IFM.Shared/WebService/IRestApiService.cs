using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public interface IRestApiService
    {
        void ExecuteRestApi(Action executeAction);
        TResult ExecuteRestApi<TResult>(Action<ResultValue<TResult>> executeAction);
    }
}
