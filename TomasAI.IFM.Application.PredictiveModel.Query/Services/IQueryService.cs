using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;


namespace TomasAI.IFM.Application.PredictiveModel.Query.Services
{
    public interface IQueryService
    {
        Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(IQuery<TResult> query) where TResult : class;
    }
}
