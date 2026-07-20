using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Telemetry.ViewModels;
using TomasAI.IFM.Shared.Telemetry.ServiceApi;

namespace TomasAI.IFM.Application.Telemetry.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LogController : ControllerBase
    {
        readonly ITelemetryCommandApi _commandApi;

        public LogController(ITelemetryCommandApi commandApi) 
        {
            _commandApi = IsArgumentNull.Set(commandApi);
        }

        [HttpPost()]
        [Route("LogEvents")]
        public async Task< StatusCodeResult> PostLogEventsAsync(LogEventsReadModel logEvents)
        {
            try 
            {
                await _commandApi.AddLogEventsAsync(logEvents);   
            }
            catch 
            {
                return BadRequest();
            }
            return Ok();
        }

    }

}
