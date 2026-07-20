using System;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;

using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.PredictiveModel.Query.Services
{
    public class QueryService : IQueryService
    {
        const string ERR_QueryService_NullQueryParameter = "QueryService.Execute query parameter is null";
        const string ERR_QueryService_QueryHandlerNotRegistered = "QueryService.Execute no query handler registered for query handler type: '{0}'";

        IQueryHandlerResolver _queryHandlerResolver;
        readonly ILogger<QueryService> _logger;

        public QueryService(IQueryHandlerResolver queryHandlerResolver, ILogger<QueryService> logger)
        {
            _queryHandlerResolver = IsArgumentNull.Set(queryHandlerResolver);
            _logger = logger;   
        }

        /// <summary>
        /// execute query asynchronously
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="query">query object</param>
        /// <returns></returns>
        public async Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(IQuery<TResult> query) where TResult : class
        {
            ServiceResult<TResult> queryResult;
            try
            {
                // execute query...
                var sw = new Stopwatch();
                sw.Start();
                var queryHandlerType = typeof(IAsyncQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
                dynamic queryHandler = GetQueryHandler(query, queryHandlerType);
                queryResult = new ServiceOk<TResult>(await queryHandler.ExecuteAsync((dynamic)query));
                sw.Stop();
                var queryElapsedTime = sw.Elapsed.ToString(@"ss\.fff");
                _logger.LogInformationEvent(queryHandlerType.Name, $"{query.GetType().Name} {HttpStatusCode.OK} executed in {queryElapsedTime} seconds");
            }
            catch (Exception ex)
            {
                var errorCode = query?.ErrorCode ?? -1;
                queryResult = new ServiceFailed<TResult>(errorCode, ex.Message);
                _logger.LogErrorEvent(this.GetType().Name, ex, $"{query.GetType().Name} {HttpStatusCode.BadRequest}  failed due to {queryResult.ErrorMessage}");
            }
            return queryResult;
        }

        /// <summary>
        /// return async query handler for query
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="query"></param>
        /// <param name="queryHandlerType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
       object GetQueryHandler<TResult>(IQuery<TResult> query, Type queryHandlerType) where TResult : class
        {
            // query cannot be null...
            if (query is null)
                throw new ArgumentException(ERR_QueryService_NullQueryParameter);

            // instantiate query handler...
            var queryHandler = _queryHandlerResolver.Resolve(queryHandlerType) ;
            if (queryHandler is null)
            {
                string errorMsg = string.Format(ERR_QueryService_QueryHandlerNotRegistered, queryHandlerType);
                throw new InvalidOperationException(errorMsg);
            }
            return queryHandler;
        }

    }
}