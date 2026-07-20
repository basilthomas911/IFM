using System.Net;
using Microsoft.AspNetCore.Mvc;
using TomasAI.IFM.Application.PredictiveModel.Query.Services;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.PredictiveModel.Server.Controllers
{
    public abstract class QueryControllerBase : ControllerBase
    {
        readonly IQueryService _queryService;
        readonly ILogger _logger;

        protected abstract string ControllerName { get; }

        public QueryControllerBase(IQueryService queryService, ILogger logger)
        {
            _queryService = IsArgumentNull.Set(queryService);
            _logger = IsArgumentNull.Set(logger);
        }

        protected async Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(IQuery<TResult> query) where TResult : class
        {
            var queryTypeName = query.GetType().Name;
            try
            {
                var queryResult = await _queryService.ExecuteQueryAsync(query);
                Response.StatusCode = (int)(queryResult.Success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);
                return queryResult;
            }
            catch (Exception ex)
            {
                _logger.LogErrorEvent($"{ControllerName}", ex, $": {queryTypeName} failed");
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return new ServiceFailed<TResult>(query.ErrorCode, ex.Message);
            }
        }

    }
}

