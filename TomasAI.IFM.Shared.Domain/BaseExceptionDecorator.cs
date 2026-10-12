using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TomasAI.IFM.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.Domain;

/// <summary>
/// base exception decorator constructor
/// </summary>
/// <param name="eventProducer"></param>
/// <param name="logger"></param>
public abstract class BaseExceptionDecorator<TState>(IEventProducer eventProducer, ILogger logger)
    : IExceptionCommandDecorator<TState> where TState : IBoundedContextState
{
    public abstract Task<IErrorEvent> ConvertExceptionToErrorEventAsync(ICommand command, Exception ex);

    /// <summary>
    /// post error event via event producer
    /// </summary>
    /// <param name="errorEvent"></param>
    /// <returns>posted error event</returns>
    public async Task<IErrorEvent> PostErrorEventAsync(IErrorEvent errorEvent)
    {
        ArgumentNullException.ThrowIfNull(errorEvent);
        global::TomasAI.IFM.Shared.Domain.PersistenceDiagnosticLog.Write1(logger, null, errorEvent.CommandName);
        await (eventProducer.PostEventAsync(errorEvent)).ConfigureAwait(false);
        return errorEvent;
    }

    /// <summary>
    /// convert command exception and post command exception event
    /// </summary>
    /// <param name="command"></param>
    /// <param name="e"></param>
    /// <returns>command exception event</returns>
    protected async Task<IErrorEvent> ConvertCommandExceptionToErrorEventAsync(ICommand command, Exception e)
        => e switch
        {
            CommandValidationException ex => await (PostErrorEventAsync(GetCommandExceptionEvent(ErrorType.CommandValidation, command, ex))).ConfigureAwait(false),
            StorageException ex => await (PostErrorEventAsync(GetCommandExceptionEvent(ErrorType.Storage, command, ex))).ConfigureAwait(false),
            CommandException ex => await (PostErrorEventAsync(GetCommandExceptionEvent(ErrorType.Command, command, ex))).ConfigureAwait(false),
            ConcurrencyException ex => await (PostErrorEventAsync(GetCommandExceptionEvent(ErrorType.Concurrency, command, ex))).ConfigureAwait(false),
            _ => await (PostErrorEventAsync(GetCommandExceptionEvent(ErrorType.Undefined, command, e))).ConfigureAwait(false)
        };

    /// <summary>
    /// return command failed event
    /// </summary>
    /// <typeparam name="TFailedEvent"></typeparam>
    /// <param name="command"></param>
    /// <param name="ex"></param>
    /// <returns>command failed event</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Best-effort diagnostic enrichment must preserve the original command failure.")]
    public TFailedEvent GetCommandFailedEvent<TFailedEvent>(ICommand command, Exception ex) where TFailedEvent : IErrorEvent
    {
        ArgumentNullException.ThrowIfNull(ex);
        var e = Activator.CreateInstance<TFailedEvent>();
        if (command is not null)
        {
            EventInitHelper.SetProperty(e, nameof(IErrorEvent.CommandId), command.CommandId);
            EventInitHelper.SetProperty(e, nameof(IErrorEvent.CommandName), command.GetType().Name);
            EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorType), ErrorType.Command);
            EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorMessage), ex.Message);
            EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorCode), command.ErrorCode);
            EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorData), $"{ex}");
            try { EventInitHelper.SetProperty(e, nameof(IEvent.AggregateId), command.StreamId); } catch { /* Diagnostic enrichment failure must not replace the original command exception. */ }
            try { EventInitHelper.SetProperty(e, nameof(IErrorEvent.CommandData), JsonConvert.SerializeObject(command, Formatting.Indented)); } catch { /* Diagnostic enrichment failure must not replace the original command exception. */ }
        }
        else
        {
            EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorType), ErrorType.Command);
            EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorMessage), ex.Message);
            EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorData), $"{ex}");
        }
        return e;
    }

    /// <summary>
    /// return command exception event
    /// </summary>
    /// <param name="errorType"></param>
    /// <param name="command"></param>
    /// <param name="ex"></param>
    /// <returns>command exception event</returns>
    public CommandExceptionEvent GetCommandExceptionEvent(ErrorType errorType, ICommand command, Exception ex)
    {
        var e = GetCommandFailedEvent<CommandExceptionEvent>(command, ex);
        EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorType), errorType);
        return e;
    }

}
