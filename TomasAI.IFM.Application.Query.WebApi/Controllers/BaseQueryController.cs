using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TomasAI.IFM.Application.Services;

namespace TomasAI.IFM.Application.Query.WebApi.Controllers
{
    public class BaseQueryController : Controller
    {
        protected async Task<JsonResult> GetServiceResultAsync<TResult>(Func<Task<ServiceResult<TResult>>> serviceResultFunc)
        {
            var serviceResult = await serviceResultFunc();
            return (serviceResult is ServiceOk<TResult>)
                ? new JsonResult(serviceResult) { StatusCode = 200 }
                : new JsonResult(serviceResult) { StatusCode = 409 };
        }
    }
}