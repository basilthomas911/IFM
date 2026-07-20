using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FuturesItiTrendController : CommandControllerBase
    {
        /// <summary>
        /// futures iti trend controller constructor
        /// </summary>
        /// <param name="commandService"></param>
        public FuturesItiTrendController(ICommandService commandService, ILogger<FuturesItiTrendController> logger) 
            : base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("BuildFuturesItiTrendModel")]
        [Route("LoadFuturesItiTrendDeltaModelData")]
        [Route("LoadFuturesItiTrendClassModelData")]
        [Route("TrainFuturesItiTrendDeltaModel")]
        [Route("TrainFuturesItiTrendClassModel")]
        public async Task PostFuturesItiTrendCommandsAsync()
            => await PostCommandAsync();
    }
}