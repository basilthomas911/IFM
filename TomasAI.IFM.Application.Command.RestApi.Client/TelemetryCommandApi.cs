using TomasAI.IFM.Shared.Telemetry.Commands;
using TomasAI.IFM.Shared.Telemetry.ServiceApi;
using TomasAI.IFM.Shared.Telemetry.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Command.Client;

/// <summary>
/// telemetry command api constructor
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class TelemetryCommandApi(ICommandService commandSvc) : ITelemetryCommandApi
{
    const string TelemetryController = "Telemetry";
    readonly ICommandService _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// add log events to telemetry log storage
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddLogEventsAsync(LogEventsReadModel logEvents)
        => await new AddLogEventsCommand( logEvents)
            .SetCommandId(_ => { })
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, TelemetryController));

}
