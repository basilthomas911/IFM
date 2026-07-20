using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Domain;

public class CommandLoggerDecorator<TState>(ICommandContext<TState> commandContext, ILogger logger) 
    : ICommandContext<TState> where TState : IBoundedContextState
{
    /// <summary>
    /// Executes the specified command asynchronously and returns the result.
    /// </summary>
    /// <remarks>Logs the execution time of the command unless the command is routed to
    /// "TelemetryLogsAggregate".</remarks>
    /// <typeparam name="TEntityId">The type of the entity identifier associated with the command.</typeparam>
    /// <param name="command">The command to be executed. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult{Guid}"/>
    /// indicating the outcome of the command execution.</returns>
    public async Task<ServiceResult<Guid>> ExecuteAsync<TEntityId>(ICommand<TEntityId> command) where TEntityId : IActorEntityId
    {
        var sw = new Stopwatch();
        sw.Start();
        var serviceResult = await commandContext.ExecuteAsync(command);
        sw.Stop();
        var queryElapsedTime = sw.Elapsed.ToString(@"ss\.fff");
        if (! $"{command.RouteTo}".Equals("TelemetryLogsAggregate"))
            logger.LogInformationEvent(command.CommandName, "{CommandName} executed in {QueryElapsedTime} seconds", command.CommandName, queryElapsedTime);
        return serviceResult;
    }

    /// <summary>
    /// Executes the specified command asynchronously and returns the result.
    /// </summary>
    /// <remarks>Logs the execution time of the command unless the command is routed to
    /// "TelemetryLogsAggregate".</remarks>
    /// <param name="command">The command to be executed. Must not be null.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, containing a <see
    /// cref="ServiceResult{Guid}"/> with the execution result.</returns>
    public async ValueTask<ServiceResult<Guid>> ExecuteAsync(ICommand command)
    {
        var sw = new Stopwatch();
        sw.Start();
        var serviceResult = await commandContext.ExecuteAsync(command);
        sw.Stop();
        var queryElapsedTime = sw.Elapsed.ToString(@"ss\.fff");
        if (!$"{command.RouteTo}".Equals("TelemetryLogsBoundedContext"))
            logger.LogInformationEvent(command.CommandName, "{CommandName} executed in {QueryElapsedTime} seconds", command.CommandName, queryElapsedTime);
        return serviceResult;
    }
}
