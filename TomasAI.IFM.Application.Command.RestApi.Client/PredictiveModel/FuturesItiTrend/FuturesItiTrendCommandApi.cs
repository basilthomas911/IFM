using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Commands;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;

namespace TomasAI.IFM.Application.Command.Client.PredictiveModel.FuturesItiTrend;

/// <summary>
/// application command api constructor
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class FuturesItiTrendCommandApi(ICommandService commandSvc) : IFuturesItiTrendCommandApi
{
    const string ApplicationController = "FuturesItiTrend";
    readonly ICommandService _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// build futures iti trend model
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> BuildFuturesItiTrendModelAsync(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly    endDate)
        => await new BuildFuturesItiTrendModelCommand( symbol, valueDate, startDate, endDate)
            .SetCommandId(_ => { })
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ApplicationController));

    /// <summary>
    /// load futures iti trend delta model data
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> LoadFuturesItiTrendDeltaModelDataAsync(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate)
        => await new LoadFuturesItiTrendDeltaModelDataCommand(symbol, valueDate, startDate, endDate)
            .SetCommandId(_ => { })
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ApplicationController));

    /// <summary>
    /// load futures iti trend class model data
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> LoadFuturesItiTrendClassModelDataAsync(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate)
        => await new LoadFuturesItiTrendClassModelDataCommand( symbol, valueDate, startDate, endDate)
            .SetCommandId(_ => { })
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ApplicationController));

    /// <summary>
    /// train futures iti trend delta model
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="statistics"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> TrainFuturesItiTrendDeltaModelAsync(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate, FuturesItiTrendModelDataStatistics statistics)
        => await new TrainFuturesItiTrendDeltaModelCommand( symbol, valueDate, startDate,  endDate, statistics)
            .SetCommandId(_ => { })
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ApplicationController));

    /// <summary>
    /// train futures iti trend class model
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="statistics"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> TrainFuturesItiTrendClassModelAsync(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate, FuturesItiTrendModelDataStatistics statistics)
        => await new TrainFuturesItiTrendClassModelCommand (symbol, valueDate, startDate, endDate, statistics)
            .SetCommandId(_ => { })
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ApplicationController));
}
