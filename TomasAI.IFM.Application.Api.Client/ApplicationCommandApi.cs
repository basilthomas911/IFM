using TomasAI.IFM.Shared.Application.CommandParameters;
using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.Application.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// application command api constructor
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class ApplicationCommandApi(ICommandServiceApi commandSvc) 
    : IApplicationCommandApi
{
    readonly ICommandServiceApi _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// start application
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartApplicationAsync(DateOnly valueDate)
        => await new StartApplicationCommand(valueDate)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(ApplicationUriPath.Start, e));

    /// <summary>
    /// shutdown application
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ShutdownApplicationAsync(DateOnly valueDate)
        => await new ShutdownApplicationCommand(valueDate)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(ApplicationUriPath.Shutdown, e));

}
