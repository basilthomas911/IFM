using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FundController(ICommandService commandService, ILogger<FundController> logger)
    : CommandControllerBase(commandService, logger)
{
    [HttpPost]
    [Route("CreateFund")]
    [SwaggerOperation(Summary = "create fund")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> ExecuteCreateFundAsync(CreateFundCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("AddOrderToFund")]
    [SwaggerOperation(Summary = "start application")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> ExecuteAddOrderToFundAsync(AddOrderToFundCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("AddTradeToFundOrder")]
    [SwaggerOperation(Summary = "start application")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> ExecuteAddTradeToFundOrderAsync(AddTradeToFundOrderCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("ChangeFundOrderTradeState")]
    [SwaggerOperation(Summary = "start application")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> ExecuteChangeFundOrderTradeStateAsync(ChangeFundOrderTradeStateCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("RemoveOrderFromFund")]
    [SwaggerOperation(Summary = "start application")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> ExecuteRemoveOrderFromFundAsync(RemoveOrderFromFundCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("RemoveTradeFromFundOrder")]
    [SwaggerOperation(Summary = "start application")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> ExecuteRemoveTradeFromFundOrderAsync(RemoveTradeFromFundOrderCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("CloseFundOrder")]
    [SwaggerOperation(Summary = "start application")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> ExecuteCloseFundOrderAsync(CloseFundOrderCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("GenerateFundMaxProfit")]
    [SwaggerOperation(Summary = "start application")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> GenerateFundMaxProfitAsync(GenerateFundMaxProfitCommand command)
        => await PostCommandAsync(command);

}