using System.Net;
using Microsoft.AspNetCore.Mvc;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Query.Server.Controllers;

public abstract class QueryControllerBase(IQueryService queryService, ILogger logger) : ControllerBase
{
    readonly IQueryService _queryService = IsArgumentNull.Set(queryService)!;
    readonly ILogger _logger = IsArgumentNull.Set(logger)!;
    readonly IJsonSerializer _serializer = new NewtonSoftJsonSerializer();

    protected abstract string ControllerName { get; }

    protected async Task<ServiceResult<TResult>> ExecuteQueryAsync<TResult>(IQuery<TResult> query) where TResult : class
    {
        var queryTypeName = query.GetType().Name;
        try
        {
            var queryResult = await _queryService.ExecuteQueryAsync(query);
            Response.StatusCode = (int )(queryResult.Success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);
            return queryResult;
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent($"{ControllerName}", ex, $": {queryTypeName} failed");
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return new ServiceFailed<TResult>(query.ErrorCode, ex.Message);
        }
    }

    protected async Task ExecuteQueryAsync()
    {
        string content;
        var queryType = default(Type);
        try
        {
            var queryTypeName = Request.Headers.ContainsKey("X-QueryTypeName")
                ? default
                : Request.Headers["X-QueryTypeName"][0];
            queryType = Type.GetType(queryTypeName!);
            var sr = new StreamReader(Request.Body);
            var serializedQuery = await sr.ReadToEndAsync();
            var query = _serializer.Deserialize(serializedQuery, queryType!);
            var queryResult = await _queryService.ExecuteQueryAsync((dynamic)query) as ServiceResult;
            content = _serializer.Serialize(queryResult!);
            if (queryResult!.Success)
                Response.StatusCode = (int)HttpStatusCode.OK;
            else
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            Response.StatusCode = (int)(queryResult.Success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent($"{ControllerName}", ex, $": {queryType!.Name} failed");
            var errorResult = new ServiceResult(false, 1456, ex.Message);
            content = _serializer.Serialize(errorResult);
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        Response.ContentType = _serializer.ContentType;
        await Response.WriteAsync(content);
    }

}

