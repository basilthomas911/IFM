using System;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Services.Application
{
    /// <summary>Provides the ApplicationEventService UI service boundary.</summary>
    public class ApplicationEventService : UiServiceBase<ApplicationEventService>
    {
        readonly IApplicationUIEventConsumer _applicationEventConsumer;

        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public ApplicationEventService(IApplicationUIEventConsumer applicationEventConsumer)
        {
            _applicationEventConsumer = applicationEventConsumer ?? throw new ArgumentNullException(nameof(applicationEventConsumer));
        }

        /// <summary>
        /// start listening for application events
        /// </summary>
        /// <param name="startupAction"></param>
        /// <param name="shutdownAction"></param>
        public async Task StartApplicationEventConsumerAsync(
            Func<ApplicationStartupEvent, ValueTask> startupAction,
            Func<ApplicationShutdownEvent, ValueTask> shutdownAction)
            => await _applicationEventConsumer.StartAsync(startupAction, shutdownAction);

        /// <summary>
        /// stop listening for application events
        /// </summary>
        public async Task StopApplicationEventConsumerAsync() 
            => await _applicationEventConsumer.StopAsync();
    }
}
