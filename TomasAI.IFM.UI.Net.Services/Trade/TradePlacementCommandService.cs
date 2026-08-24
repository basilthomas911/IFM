using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

namespace TomasAI.IFM.UI.Net.Services.Trade;

/// <summary>
/// create trade command model
/// </summary>
/// <param name="commandApi"></param>
public class TradePlacementCommandService(
    ITradePlacementCommandApi commandApi) : UiServiceBase<TradePlacementCommandService>
{
    readonly ITradePlacementCommandApi _commandApi = IsArgumentNull.Set(commandApi)!;

    /// <summary>
    /// start trade placement signal service
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public Task<Guid> StartTradePlacementAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
        => ExecuteCommandAsync(() => _commandApi.StartTradePlacementAsync(
            new(contractId, valueDate),
            cancellationToken));

    /// <summary>
    /// stop trade placement signal service
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    public Task<Guid> StopTradePlacementAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
        => ExecuteCommandAsync(() => _commandApi.StopTradePlacementAsync(
            new(contractId, valueDate),
            cancellationToken));

}
