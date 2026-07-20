using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Application.Events;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.Models
{
    public class ApplicationEventModel : BaseModel<ApplicationEventModel>
    {
        readonly IApplicationUIEventConsumer _applicationEventConsumer;

        public ApplicationEventModel(IApplicationUIEventConsumer applicationEventConsumer)
        {
            _applicationEventConsumer = applicationEventConsumer ?? throw new ArgumentNullException(nameof(applicationEventConsumer));
        }

        /// <summary>
        /// start listening for application events
        /// </summary>
        /// <param name="startupAction"></param>
        /// <param name="shutdownAction"></param>
        public async Task StartApplicationEventConsumerAsync(
            Action<ApplicationStartupEvent>? startupAction = null,
            Action<ApplicationShutdownEvent>? shutdownAction = null)
            => await _applicationEventConsumer.StartAsync(startupAction!, shutdownAction!);

        /// <summary>
        /// stop listening for application events
        /// </summary>
        public async Task StopApplicationEventConsumerAsync() 
            => await _applicationEventConsumer.StopAsync();
    }
}
