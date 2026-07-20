using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging
{
    public interface IMarketDataService
    {
        Task<ServiceResult<TResult>> PostAsync<TResult, TRequest>(TRequest request, string controllerName);
        Task<ServiceResult> PostAsync<TRequest>(TRequest request, string controllerName);
    }
}
