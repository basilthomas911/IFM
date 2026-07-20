using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Commands;

namespace TomasAI.IFM.Application.Command.Server.Controllers;

/// <summary>
/// Provides endpoints for managing market data, including futures contracts, futures option contracts, and yield curve
/// rates. This controller allows clients to add, modify, remove, and import market data entities through HTTP POST
/// requests.
/// </summary>
/// <remarks>Each action in this controller is designed to handle a specific type of market data operation, such
/// as adding or changing futures contracts or yield curve rates. The controller uses commands to encapsulate the data
/// and logic required for these operations, and returns a <see cref="ServiceResult{T}"/> containing the result of the
/// operation.   The controller is built on top of the <see cref="CommandControllerBase"/> class, which provides shared
/// functionality for executing commands and handling results. All endpoints produce JSON responses and return
/// appropriate HTTP status codes to indicate success or failure.</remarks>
/// <param name="commandService"></param>
/// <param name="logger"></param>
[Route("api/[controller]")]
[ApiController]
public class MarketDataController(ICommandService commandService, ILogger<MarketDataController> logger) 
    : CommandControllerBase(commandService, logger)
{
    [HttpPost]
    [Route("AddFuturesContract")]
    [SwaggerOperation(Summary = "add futures contract")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(400, "bad request", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(409, "conflict", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> AddFuturesContractAsync(AddFuturesContractCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("ChangeFuturesContract")]
    [SwaggerOperation(Summary = "change futures contract")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> ChangeFuturesContractAsync(ChangeFuturesContractCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("RemoveFuturesContract")]
    [SwaggerOperation(Summary = "remove futures contract")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> RemoveFuturesContractAsync(RemoveFuturesContractCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("AddFuturesOptionContract")]
    [SwaggerOperation(Summary = "add futures option contract")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> AddFuturesOptionContractAsync(AddFuturesOptionContractCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("AddFuturesOptionContracts")]
    [SwaggerOperation(Summary = "add futures option contracts")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> AddFuturesOptionContractsAsync(AddFuturesOptionContractsCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("ChangeFuturesOptionContract")]
    [SwaggerOperation(Summary = "change futures option contract")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> ChangeFuturesOptionContractAsync(ChangeFuturesOptionContractCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("RemoveFuturesOptionContract")]
    [SwaggerOperation(Summary = "remove futures option contract")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> RemoveFuturesOptionContractAsync(RemoveFuturesOptionContractCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("AddYieldCurveRate")]
    [SwaggerOperation(Summary = "add yield curve rate")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> AddYieldCurveRateAsync(AddYieldCurveRateCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("ChangeYieldCurveRate")]
    [SwaggerOperation(Summary = "change yield curve rate")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> ChangeYieldCurveRateAsync(ChangeYieldCurveRateCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("RemoveYieldCurveRate")]
    [SwaggerOperation(Summary = "remove yield curve rate")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> RemoveYieldCurveRateAsync(RemoveYieldCurveRateCommand command)
        => await ExecuteCommandAsync(command);

    [HttpPost]
    [Route("ImportYieldCurveRates")]
    [SwaggerOperation(Summary = "import yield curve rates")]
    [SwaggerResponse(200, "success", typeof(ServiceResult<Guid>))]
    [SwaggerResponse(500, "failure")]
    [Produces("application/json")]
    public async Task<ServiceResult<Guid>> ImportYieldCurveRatesAsync(ImportYieldCurveRatesCommand command) 
        => await ExecuteCommandAsync(command);

}