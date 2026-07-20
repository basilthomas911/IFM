using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventChannel;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;

namespace TomasAI.IFM.Application.Command;

public class CommandService : ICommandService
{
    const string ERR_CommandContextHandlerNotRegistered = "CommandActionBlock.GetCommandHandler: no command context registered for command handler type: '{0}'";
    static ConcurrentDictionary<Type, object>? _cmdCtxHndlrMap;
    static ConcurrentDictionary<string, ConcurrentAsyncEventChannel< (ICommand Command, Type CommandContextType, string CommandBoundedContextName)>>? _cmdChannelMap;
    readonly Func<(ICommand Command, Type CommandContextType, string CommandBoundedContextName), ValueTask> _cmdHandlerFunc;
    readonly ICommandHandlerResolver _cmdCtxHndlrResolver;
    readonly IExceptionEventProducer _exceptionEventProducer;
    readonly ILogger<EventChannel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandService"/> class, which is responsible for  managing and
    /// executing commands within the system.
    /// </summary>
    /// <remarks>The <see cref="CommandService"/> is designed to manage the execution of commands and
    /// maintain mappings  between bounded context names and their corresponding state types. It also initializes internal
    /// structures  for command handling and logging. The service is a core component for coordinating command
    /// processing  in the system.  The <see cref="BoundedContextStateTypeMap"/> property is pre-populated with mappings
    /// between bounded context names  and their associated state types. These mappings are used to determine the type of
    /// state associated  with a given bounded context during command execution.  This constructor ensures that all
    /// required dependencies are provided and initializes the service  with default configurations.</remarks>
    /// <param name="commandHandlerResolver">The resolver used to locate and provide instances of command handlers. This parameter cannot be null.</param>
    /// <param name="logger">The logger used to log diagnostic and operational information for the <see cref="CommandService"/>.  This
    /// parameter cannot be null.</param>
    public CommandService(ICommandHandlerResolver commandHandlerResolver, ILogger<EventChannel> logger)
    {
        _cmdCtxHndlrResolver = IsArgumentNull.Set(commandHandlerResolver);
        _logger = IsArgumentNull.Set(logger);
        _cmdCtxHndlrMap ??= new ();
        _cmdChannelMap ??= new ();
        _cmdHandlerFunc =  e => ExecuteCommandOnEventChannelAsync(e.Command, e.CommandContextType, e.CommandBoundedContextName);

        BoundedContextStateTypeMap = new Dictionary<BoundedContextName, Type>
        {
             {BoundedContextName.FuturesTradeSignalBoundedContext, typeof(FuturesTradeSignalCommandState) },
            {BoundedContextName.FuturesRsiSignalBoundedContext, typeof(FuturesRsiSignalCommandState) },
            {BoundedContextName.FuturesTdiSignalBoundedContext, typeof(FuturesTdiSignalCommandState) },
            {BoundedContextName.FuturesItiSignalBoundedContext, typeof(FuturesItiSignalCommandState) },
        };
        _logger.LogInformationEvent("CommandService", "CommandService initialization completed");
    }

    /// <summary>
    /// Gets a mapping of bounded context names to their corresponding state types.
    /// </summary>
    /// <remarks>This property provides a way to associate bounded context names with their respective state
    /// types,  enabling dynamic resolution of state types based on bounded context names.</remarks>
    public Dictionary<BoundedContextName, Type> BoundedContextStateTypeMap { get; }

    /// <summary>
    /// Asynchronously posts a command to the appropriate command event channel and returns the result.
    /// </summary>
    /// <remarks>This method logs any exceptions that occur during the operation and posts an error
    /// event using the exception event producer.</remarks>
    /// <typeparam name="TBoundedContextState">The type of the bounded context state associated with the command. Must implement <see
    /// cref="IBoundedContextState{TBoundedContextState}"/>.</typeparam>
    /// <param name="command">The command to be posted. The command must have a valid <see cref="ICommand.StreamId"/>.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the <see cref="Guid"/> of the command if successful, or an error
    /// result if the operation fails.</returns>
    public async Task<ServiceResult<Guid>> PostAsync<TBoundedContextState>(ICommand command) where TBoundedContextState 
        : IBoundedContextState<TBoundedContextState>
    {
        try
        {
            var streamId = IsArgumentNull.Set(command.StreamId);
            var cmdEventChannel = GetCommandEventChannel(streamId);
            var commandContextType = typeof(ICommandContext<>).MakeGenericType(typeof(TBoundedContextState));
            cmdEventChannel.WriteData((command, commandContextType, commandContextType.GetType().Name));
            return new ServiceOk<Guid>(command.CommandId);
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent("CommandService", ex, "CommandService.PostAsync failed for command: {CommandName}", command.CommandName);
            var errorEvent = new CommandExceptionEvent().ToErrorEvent(command, ex);
            return new ServiceFailed<Guid>(errorEvent);
        }
    }

    /// <summary>
    /// Executes the specified command asynchronously within the context of a bounded context.
    /// </summary>
    /// <remarks>This method dynamically determines the command context type based on the specified
    /// bounded context and executes the command within that context. The method is designed to be used with
    /// commands that are part of a bounded context architecture.</remarks>
    /// <typeparam name="TBoundedContext">The type of the bounded context state, which must implement <see
    /// cref="IBoundedContextState{TBoundedContext}"/>.</typeparam>
    /// <param name="command">The command to be executed. This parameter cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see
    /// cref="ServiceResult{Guid}"/> which includes the unique identifier of the executed command.</returns>
    public async Task<ServiceResult<Guid>> ExecuteAsync<TBoundedContext>(ICommand command) 
        where TBoundedContext : IBoundedContextState<TBoundedContext>
    {
        var commandContextType = typeof(ICommandContext<>).MakeGenericType(typeof(TBoundedContext));
        var commandContextName = commandContextType.GetType().FullName;
        return await ExecuteCommandAsync(command, commandContextType, commandContextName!);
    }

    /// <summary>
    /// Executes the specified command asynchronously within a given command context.
    /// </summary>
    /// <remarks>This method logs the execution process and handles exceptions by logging errors and
    /// posting an error event.</remarks>
    /// <param name="command">The command to be executed. Must implement the <see cref="ICommand"/> interface.</param>
    /// <param name="commandContextType">The type of the command context handler that processes the command.</param>
    /// <param name="commandContextName">The name of the command context, used to identify the specific context instance.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> representing the result of the command execution. Contains the unique
    /// identifier of the command if successful, or an error result if the execution fails.</returns>
    async ValueTask<ServiceResult<Guid>> ExecuteCommandAsync(ICommand command, Type commandContextType, string commandContextName)
    {
        ServiceResult<Guid> serviceResult;
        try
        {
            _logger.LogInformationEvent("CommandService", "Executing command: {CommandName} on event stream: {StreamId}", command.CommandName, command.StreamId);
            var cmdCtxHndlr = GetCommandContextHandler(commandContextType, commandContextName);
            serviceResult = (await ((dynamic)cmdCtxHndlr).ExecuteAsync((dynamic)command) as ServiceResult<Guid>)!;
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent("CommandService", ex, "CommandService.ExecuteCommandAsync failed for command: {CommandName} on event stream: {StreamId}", command.CommandName, command.StreamId);
            var errorEvent = new CommandExceptionEvent { }.ToErrorEvent(command, ex);
            serviceResult = new ServiceFailed<Guid>(errorEvent);
        }
        return serviceResult;
    }

    /// <summary>
    /// Executes a specified command on an event channel asynchronously.
    /// </summary>
    /// <remarks>This method logs the execution of the command and handles any exceptions that occur
    /// during execution by logging the error and posting an error event.</remarks>
    /// <param name="command">The command to be executed. This parameter cannot be null.</param>
    /// <param name="commandContextType">The type of the command context. This parameter is used to determine the appropriate handler for the
    /// command.</param>
    /// <param name="commandContextName">The name of the command context. This parameter is used to identify the specific context within which the
    /// command should be executed.</param>
    /// <returns></returns>
    async ValueTask ExecuteCommandOnEventChannelAsync(ICommand command, Type commandContextType, string commandContextName)
    {
        try
        {
            _logger.LogInformationEvent("CommandService", "Executing command: {CommandName} on event stream: {StreamId}", command.CommandName, command.StreamId);
            var cmdCtxHndlr = GetCommandContextHandler(commandContextType, commandContextName);
            await ((dynamic)cmdCtxHndlr).ExecuteAsync((dynamic)command);
        }
        catch (Exception ex)
        {
            _logger.LogErrorEvent("CommandService", ex, "CommandService.ExecuteCommandAsync failed for command: {CommandName} on event stream: {StreamId}", command.CommandName, command.StreamId);
            var errorEvent = new CommandExceptionEvent { }.ToErrorEvent(command, ex);
            await _exceptionEventProducer.PostEventAsync(errorEvent);
        }
    }

    /// <summary>
    /// Retrieves the command channel associated with the specified event stream ID, creating a new one if it does
    /// not already exist.
    /// </summary>
    /// <remarks>The returned command channel is responsible for processing commands associated with
    /// the specified event stream. If a new channel is created, it is initialized and started
    /// automatically.</remarks>
    /// <param name="streamId">The unique identifier of the event stream for which the command channel is requested. Cannot be null or
    /// empty.</param>
    /// <returns>A <see cref="ConcurrentAsyncEventChannel{T}"/> instance that handles commands for the specified event stream. If
    /// a channel does not already exist for the given <paramref name="streamId"/>, a new one is created and
    /// returned.</returns>
    ConcurrentAsyncEventChannel<(ICommand Command, Type CommandContextType, string CommandBoundedContextName)> GetCommandEventChannel(string streamId)
    {
        if (!_cmdChannelMap!.ContainsKey(streamId))
        {
            _logger.LogInformationEvent("CommandService", "Creating new command channel for event stream id: {StreamId}", streamId);
            var newChannel = new ConcurrentAsyncEventChannel<(ICommand Command, Type CommandContextType, string CommandBoundedContextName)>(
                $"CommandService:{streamId}", 
                _cmdHandlerFunc,
                _logger);
            newChannel.Start();
            _cmdChannelMap.TryAdd(streamId, newChannel);
        }
        return _cmdChannelMap[streamId];
    }

    /// <summary>
    /// return aggregate command handler
    /// </summary>
    /// <param name="commandContextType"></param>
    /// <param name="aggregateTypeName"></param>
    /// <returns></returns>
    object GetCommandContextHandler(Type commandContextType, string aggregateTypeName)
    {
        // check if command contexthandler exists...
        if (!_cmdCtxHndlrMap!.ContainsKey(commandContextType))
        {
            // load command context handler from di resolver...
            var commandContextHandler = _cmdCtxHndlrResolver.Resolve(commandContextType);
            if (commandContextHandler is null)
            {
                string errorMsg = string.Format(ERR_CommandContextHandlerNotRegistered, commandContextType);
                throw new InvalidOperationException(errorMsg);
            }
            _cmdCtxHndlrMap.TryAdd(commandContextType, commandContextHandler);
            _logger.LogInformationEvent("CommandService", "Resolving command context handler type: {CommandContextType}", commandContextHandler.GetType().Name);
        }
        return _cmdCtxHndlrMap[commandContextType];
    }

}