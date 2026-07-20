using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using System.Diagnostics;
using System.Net;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Query;

public class QueryService : IQueryService
{
    const string ERR_QueryProcessor_NullQueryParameter = "QueryProcessor.Execute query parameter is null";
    const string ERR_QueryProcessor_QueryHandlerNotRegistered = "QueryProcessor.Execute no query handler registered for query handler type: '{0}'";
    const string ERR_QueryProcessor_NullCommandParameter = "QueryProcessor.Execute command parameter is null";
    const string ERR_QueryProcessor_CommandHandlerNotRegistered = "QueryProcessor.Execute no command handler registered for command handler type: '{0}'";

    IQueryHandlerResolver _queryHandlerResolver;
    readonly ILogger<QueryService> _logger;

    public QueryService(IQueryHandlerResolver queryHandlerResolver, ILogger<QueryService> logger)
    {
        _queryHandlerResolver = queryHandlerResolver;
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
        dynamic queryHandler = default!;
        ServiceResult<TResult>? queryResult;
        try
        {
            IsArgumentNull.Check(query, ERR_QueryProcessor_NullQueryParameter);
            // execute query...
            var sw = new Stopwatch();
            sw.Start();
            var queryHandlerObj = GetQueryHandler(query, typeof(IAsyncQueryHandler<,>));
            queryHandler = queryHandlerObj;
            queryResult = new ServiceOk<TResult>(await queryHandler.ExecuteAsync((dynamic)query));
            sw.Stop();
            var queryElapsedTime = sw.Elapsed.ToString(@"ss\.fff");
            _logger.LogInformationEvent(queryHandlerObj.GetType().Name, $"{query.GetType().Name} {HttpStatusCode.OK} executed in {queryElapsedTime} seconds");
        }
        catch (Exception ex)
        {
            var errorCode = query?.ErrorCode ?? -1;
            queryResult = new ServiceFailed<TResult>(errorCode, ex.Message);
            _logger.LogErrorEvent(GetType().Name, ex, $"{query?.GetType()?.Name} {HttpStatusCode.OK}  failed due to {queryResult.ErrorMessage}");
        }
        return queryResult;

    }

    object GetQueryHandler<TResult>(IQuery<TResult> query, Type queryHandlerType) where TResult : class
    {
        // query cannot be null...
        if (query == null)
            throw new ArgumentException(ERR_QueryProcessor_NullQueryParameter);

        // instantiate query handler...
        var handlerType = queryHandlerType.MakeGenericType(query.GetType(), typeof(TResult));
        dynamic queryHandler = _queryHandlerResolver.Resolve(handlerType);

        // check if query handler exists...
        if (queryHandler == null)
        {
            string errorMsg = string.Format(ERR_QueryProcessor_QueryHandlerNotRegistered, queryHandlerType);
            throw new InvalidOperationException(errorMsg);
        }
        return queryHandler;
    }

}