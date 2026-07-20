using TomasAI.IFM.Shared.Application.CommandParameters;
using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.Application.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Command.Client;

/// <summary>
/// application command api constructor
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class ApplicationCommandApi(ICommandService commandSvc) : IApplicationCommandApi
{
    const string ApplicationController = "Application";
    readonly ICommandService _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// start application
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartApplicationAsync(StartApplicationParameter parameter)
        => await new StartApplicationCommand(parameter)
            .SetCommandId(_ => { })
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ApplicationController));

    /// <summary>
    /// shutdown application
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ShutdownApplicationAsync(ShutdownApplicationParameter parameter)
        => await new ShutdownApplicationCommand(parameter)
            .SetCommandId(_ => { })
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ApplicationController));

}
