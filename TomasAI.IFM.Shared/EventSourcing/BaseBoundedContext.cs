using System.Collections.Concurrent;
using System.Reflection;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Represents the base class for a bounded context in a domain-driven design architecture.
/// </summary>
/// <remarks>This abstract class provides the foundational structure for implementing a bounded context,
/// encapsulating the state and command execution logic. It supports command resolution and execution within the
/// context, allowing for flexible handling of domain commands.</remarks>
/// <typeparam name="TState">The type of the state associated with the bounded context. Must implement the <see
/// cref="IBoundedContextState{TState}"/> interface.</typeparam>
public abstract class BaseBoundedContext<TState> 
    : IBoundedContext<TState> where TState : class, IBoundedContextState<TState> 
{
    readonly static ConcurrentDictionary<string, (Type CmdHndlrType, object CmdHndlr, MethodInfo Command)> _boundCtxCmdHndlrMap = new();
    readonly IBoundedContextState<TState> _boundCtx;
    readonly Type _boundCtxCmdHndlrType = typeof(IBoundedContextCommandHandler<,>);
    readonly IBoundedContextCommandResolver? _boundCtxCommandResolver;

    /// <summary>
    /// base bounded context root constructor
    /// </summary>
    /// <param name="boundedContextState"></param>
    public BaseBoundedContext(IBoundedContextState<TState> boundedContextState)
    {
        _boundCtx = IsArgumentNull.Set(boundedContextState);
    }

    /// <summary>
    /// base bounded context root constructor with aggregate command resolver
    /// </summary>
    /// <param name="boundCtxState"></param>
    /// <param name="boundCtxCommandResolver"></param>
    public BaseBoundedContext(IBoundedContextState<TState> boundCtxState, IBoundedContextCommandResolver boundCtxCommandResolver)
    {
        _boundCtx = IsArgumentNull.Set(boundCtxState);
        _boundCtxCommandResolver = IsArgumentNull.Set(boundCtxCommandResolver)!;
    }

    /// <summary>
    /// bounded context state
    /// </summary>
    public IBoundedContextState<TState> State => _boundCtx;

    /// <summary>
    /// Executes the specified command within the current bounded context.
    /// </summary>
    /// <remarks>This method attempts to resolve and execute a command handler specific to the bounded
    /// context. If a suitable handler is not found, the command is executed as a derived bounded context
    /// command.</remarks>
    /// <param name="command">The command to be executed. Must not be <see langword="null"/>.</param>
    public void Execute(ICommand command)
    {
        // resolve bounded context state command handlers...
        if (!_boundCtxCmdHndlrMap.TryGetValue(command.GetType().FullName!, out var mapEntry))
        {
            var boundCtxCmdHndlrType = _boundCtxCmdHndlrType.MakeGenericType(command.GetType(), _boundCtx.GetType());
            var cmdHndlr = _boundCtxCommandResolver?.Resolve(boundCtxCmdHndlrType);
            mapEntry = (boundCtxCmdHndlrType!, cmdHndlr!, boundCtxCmdHndlrType?.GetMethod("Execute")!);
            _boundCtxCmdHndlrMap.TryAdd(command.GetType().FullName!, mapEntry);
        }

        // execute bounded context state command handler if it exists
        // otherwise execute as derived bounded context command...
        if (mapEntry.CmdHndlr is not null)
            mapEntry.Command?.Invoke(mapEntry.CmdHndlr, [command, _boundCtx]);
        else
             ((dynamic)this).Execute((dynamic)command);
    }
}
