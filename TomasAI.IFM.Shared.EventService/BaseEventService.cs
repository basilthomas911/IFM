using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Shared.EventService;

public abstract class BaseEventService(IEventServiceHandlerResolver eventHandlerResolver, ILogger logger)
    : IEventService
{
    public ILogger Logger => logger;
    protected abstract string ServiceName { get; }


    /// <summary>
    /// Executes the specified event asynchronously by resolving and invoking the appropriate event handler.
    /// </summary>
    /// <remarks>This method resolves the event handler for the given event type and invokes its execution
    /// logic.  If the event handler's execution fails, the method logs the failure and rethrows the exception. The
    /// execution time of the event is logged for monitoring purposes.</remarks>
    /// <param name="e">The event to be processed. Must not be <see langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(IEvent e)
    {
        try
        {
            var eventType = e.GetType();
            var eventTypeName = eventType.Name;
            var sw = new Stopwatch();
            sw.Start(); 
            dynamic eventHandler = eventHandlerResolver.ResolveEventHandler(eventType, this.GetType())!;
            if (! await ExecuteAsync(eventHandler, e))
                await eventHandler?.ExecuteAsync((dynamic)e);
            sw.Stop();
            var queryElapsedTime = sw.Elapsed.ToString(@"ss\.fff");
            logger.LogInformationEvent(ServiceName, $"{eventTypeName} executed in {queryElapsedTime} seconds");
        }
        catch (EventException ex)
        {
            logger.LogErrorEvent(ServiceName, ex, $"{e.GetType().Name} failed for {e.GetType().Name}");
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceName, ex, $"{e.GetType().Name} failed for {e.GetType().Name}");
        }
    }

    /// <summary>
    /// Executes the specified event asynchronously using the provided event service.
    /// </summary>
    /// <remarks>This method resolves the appropriate event handler for the given event and event service
    /// types, then invokes the handler's execution logic. It logs the execution time upon successful completion and
    /// captures any exceptions that occur during execution, logging them as errors.</remarks>
    /// <param name="e">The event to be processed. Must not be <see langword="null"/>.</param>
    /// <param name="eventService">The service responsible for handling the event. Must not be <see langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(IEvent e, IEventService eventService)
    {
        try
        {
            var eventType = e.GetType();
            var eventTypeName = eventType.Name;
            var sw = new Stopwatch();
            sw.Start();
            dynamic eventServiceHandler = eventHandlerResolver.ResolveEventServiceHandler(eventType, eventService.GetType())!;
            await eventServiceHandler?.ExecuteAsync((dynamic)e, eventService);
            sw.Stop();
            var queryElapsedTime = sw.Elapsed.ToString(@"ss\.fff");
            logger.LogInformationEvent(ServiceName, $"{eventTypeName} executed in {queryElapsedTime} seconds");
        }
        catch (EventException ex)
        {
            logger.LogErrorEvent(ServiceName, ex, $"{e.GetType().Name} failed for {e.GetType().Name}");
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceName, ex, $"{e.GetType().Name} failed for {e.GetType().Name}");
        }
    }

    protected virtual async Task<bool> ExecuteAsync(object handler, IEvent e) 
        => await Task.FromResult(false);

}
