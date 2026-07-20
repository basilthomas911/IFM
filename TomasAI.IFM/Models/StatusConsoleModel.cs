using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.Log.Events;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Service.StatusConsole;

namespace TomasAI.IFM.Models
{
    public class StatusConsoleModel : BaseModel<StatusConsoleModel>
    {
        readonly IStatusConsoleWriter _statusConsole;
        readonly IStatusConsoleEventConsumer _statusConsoleEventConsumer;

        /// <summary>
        /// create staus console controller
        /// </summary>
        /// <param name="statusConsole"></param>
        /// <param name="statusConsoleEventConsumer"></param>
        public StatusConsoleModel(IStatusConsoleWriter statusConsole, IStatusConsoleEventConsumer statusConsoleEventConsumer)
        {
            _statusConsole = statusConsole ?? throw new ArgumentNullException(nameof(statusConsole));
            _statusConsoleEventConsumer = statusConsoleEventConsumer ?? throw new ArgumentNullException(nameof(statusConsoleEventConsumer));
        }

        /// <summary>
        /// start listening for status console log updates
        /// </summary>
        /// <param name="listenerAction"></param>
        /// <param name="siteId"></param>
        public async Task StartStatusConsoleLogListenerAsync(Action<StatusConsoleLoggedEvent> listenerAction, Guid siteId)
        {
            try
            {
                await _statusConsoleEventConsumer.StartAsync(listenerAction, siteId);
            }
            catch{ }
        }

        /// <summary>
        /// stop listening for status console log updates
        /// </summary>
        /// <param name="siteId"></param>
        public async Task StopStatusConsoleLogListener(Guid siteId)
        {
            try
            {
                await _statusConsoleEventConsumer.StopAsync(siteId);
            }
            catch { }
        }

        /// <summary>
        /// write status to status console log
        /// </summary>
        /// <param name="statusSourceType"></param>
        /// <param name="statusMsg"></param>
        public async Task WriteConsoleAsync(LogSourceType statusSourceType, string statusMsg)
        {
            try
            {
                await _statusConsole.WriteConsoleAsync(statusSourceType, statusMsg);
            }
            catch { }
        }

        /// <summary>
        /// write status to status console log
        /// </summary>
        /// <param name="statusSourceType"></param>
        /// <param name="errorCode"></param>
        /// <param name="errorMsg"></param>
        public async Task WriteConsoleAsync(LogSourceType statusSourceType, int errorCode, string errorMsg)
        {
            try
            {
                await _statusConsole.WriteConsoleAsync(statusSourceType, errorCode, errorMsg);
            }
            catch { }
        }
    }
}
