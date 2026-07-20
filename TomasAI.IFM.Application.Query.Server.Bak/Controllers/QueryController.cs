using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Query.Services;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.TradeBroker.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QueryController : ControllerBase
    {
        readonly IQueryService _queryService;
        readonly NewtonSoftJsonSerializer _serializer;
        readonly ILogger<QueryController> _logger;

        public QueryController(IQueryService queryService, ILogger<QueryController> logger)
        {
            _queryService = queryService;
            _serializer = new NewtonSoftJsonSerializer();
            _logger = logger;
        }

        [HttpGet]
        public IEnumerable<string> Get()
        {
            yield return "Started Query Server in Development mode";
        }

        [HttpPost]
        [Route("{queryName}")]
        public  async Task ExecuteQueryAsync(string queryName)
        {
            ServiceResult queryResult;
            string content;
            var queryType = default(Type);
            try
            {
                var queryTypeName = Request.Headers["X-QueryTypeName"][0];
                queryType = Type.GetType(queryTypeName);
                var sr = new StreamReader(Request.Body);
                var serializedQuery = await sr.ReadToEndAsync();
                var query =_serializer.Deserialize(serializedQuery, queryType);
                queryResult = await _queryService.ExecuteQueryAsync((dynamic)query) as ServiceResult;
                content = _serializer.Serialize(queryResult);
                if (queryResult.Success)
                    Response.StatusCode = (int)HttpStatusCode.OK;
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
                Response.StatusCode = (int) (queryResult.Success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"QueryController: { queryType.Name} failed");
                var errorResult = new ServiceResult(false, 1456, ex.Message);
                content = _serializer.Serialize(errorResult);
                Response.StatusCode = (int) HttpStatusCode.InternalServerError;
            }
            Response.ContentType = _serializer.ContentType;
            await Response.WriteAsync(content);
        }
    }
}