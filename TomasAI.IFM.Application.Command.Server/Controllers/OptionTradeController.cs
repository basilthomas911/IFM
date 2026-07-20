using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OptionTradeController : CommandControllerBase
    {
        public OptionTradeController(ICommandService commandService, ILogger<OptionTradeController> logger)
            :base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("CloseOptionTrade")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> CloseOptionTradeAsync(CloseOptionTradeCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("DeleteOptionTrade")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> DeleteOptionTradeAsync(DeleteOptionTradeCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("DeleteOptionTrades")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> DeleteOptionTradesAsync(DeleteOptionTradesCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("OpenOptionTrade")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> OpenOptionTradeAsync(OpenOptionTradeCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("PlaceOptionTradeOrder")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> PlaceOptionTradeOrderAsync(PlaceOptionTradeOrderCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("ProcessOptionTradeEndOfDay")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ProcessOptionTradeEndOfDayAsync(ProcessOptionTradeEndOfDayCommand command) 
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("ChangeOptionTradeDistributionStatistics")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ChangeOptionTradeDistributionStatisticsAsync(ChangeOptionTradeDistributionStatisticsCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("ChangeOptionTradeLegData")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ChangeOptionTradeLegDataAsync(ChangeOptionTradeLegDataCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("SnapshotOptionTrade")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> SnapshotOptionTradeAsync(SnapshotOptionTradeCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("UpdateOptionTradeDailyProfitTarget")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> UpdateOptionTradeDailyProfitTargetAsync(UpdateOptionTradeDailyProfitTargetCommand command)
            => await PostCommandAsync(command);


        [HttpPost]
        [Route("InsertOptionTradeSpreadData")]
        [SwaggerOperation(Summary = "insert option trade spread data")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> InsertOptionTradeSpreadDataAsync(InsertOptionTradeSpreadDataCommand command)
            => await PostCommandAsync(command);

        [HttpPost]
        [Route("InsertOptionTradeSpreadBarData")]
        [SwaggerOperation(Summary = "insert option trade spread bar data")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> InsertOptionTradeSpreadBarDataAsync(InsertOptionTradeSpreadBarDataCommand command)
           => await PostCommandAsync(command);

        [HttpPost]
        [Route("DeleteOptionTradeSpreadBarData")]
        [SwaggerOperation(Summary = "insert option trade spread bar data")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> DeleteOptionTradeSpreadBarDataAsync(DeleteOptionTradeSpreadBarDataCommand command)
      => await PostCommandAsync(command);


        //DeleteOptionTradeSpreadBarDataAsync
    }
}