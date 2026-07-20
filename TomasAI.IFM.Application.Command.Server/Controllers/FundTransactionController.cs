using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FundTransactionController : CommandControllerBase
    {
 
        /// <summary>
        /// fund transaction controller constructor
        /// </summary>
        /// <param name="commandService"></param>
        public FundTransactionController(ICommandService commandService, ILogger<FundTransactionController> logger) : base(commandService, logger)
        {
        }

        [HttpPost]
        [Route("ChangeFundBalance")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ExecuteChangeFundBalanceAsync(ChangeFundBalanceCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("ChangeTradePnl")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ExecuteChangeTradePnlAsync(ChangeTradePnlCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("CreateFundTransaction")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ExecuteCreateFundTransactionAsync(CreateFundTransactionCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("CreateFundTransactions")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ExecuteCreateFundTransactionsAsync(CreateFundTransactionsCommand command)
            => await ExecuteCommandAsync(command);

        [HttpPost]
        [Route("ProcessEndOfDayFundTransaction")]
        [SwaggerOperation(Summary = "start application")]
        [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
        [SwaggerResponse(500, "failure")]
        [Produces("application/json")]
        public async Task<ServiceResult<Guid>> ExecuteProcessEndOfDayFundTransactionAsync(ProcessEndOfDayFundTransactionCommand command)
            => await ExecuteCommandAsync(command);

    }
}