using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade.ServiceApi;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// create trade command model
/// </summary>
/// <param name="commandApi"></param>
public class TradePlacementCommandModel(
    ITradePlacementCommandApi commandApi) : BaseModel<TradePlacementCommandModel>
{
    readonly ITradePlacementCommandApi _commandApi = IsArgumentNull.Set(commandApi)!;

    /// <summary>
    /// start trade placement signal service
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public async Task StartTradePlacementAsync(string contractId, DateOnly valueDate)
        => await ExecuteCommandAsync(() => _commandApi.StartTradePlacementAsync(new(contractId, valueDate)));

    /// <summary>
    /// stop trade placement signal service
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public async Task StopTradePlacementAsync(string contractId, DateOnly valueDate)
        => await ExecuteCommandAsync(() => _commandApi.StopTradePlacementAsync(new(contractId, valueDate)));

}
