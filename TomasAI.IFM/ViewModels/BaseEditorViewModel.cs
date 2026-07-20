using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Extensions;


namespace TomasAI.IFM.ViewModels
{
    /// <summary>
    /// base editor view model
    /// </summary>
    public class BaseEditorViewModel
    {
        readonly IAppRoot _appRoot;
 
        /// <summary>
        /// base editor view model constructor
        /// </summary>
        /// <param name="appRoot"></param>
        public BaseEditorViewModel(IAppRoot appRoot)
        {
            _appRoot = appRoot;
        }

        public IAppRoot AppRoot => _appRoot;
        public Action<int, string> OnError;
        public Action<string> ShowStatus;
        public Action ClearError;
        public Action StartWaitIndicator;
        public Action StopWaitIndicator;
        public Action<EventModel, string> ShowWaitView;

        protected async Task ExecuteCommandAsync(
            Func<Task<ServiceResult<Guid>>> commandFunc,
            Action<Guid> onSuccess,
            Action<ServiceResult<Guid>> onFailure)
        {
            var serviceResult = await commandFunc();
            if (serviceResult.Success)
                onSuccess(serviceResult.Value);
            else
                onFailure(serviceResult);
        }

        /// <summary>
        /// set event source for all consumer events
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="events"></param>
        /// <returns></returns>
        protected void SetConsumerEvents(EventTopic eventTopic, ICollection<IEvent> @events)
        {
            var eventSource = $"{eventTopic}";
            if (!@events.Any(o => o is CommandExceptionEvent))
                @events.Add(new CommandExceptionEvent {}.SetEventSource(eventSource));
            if (!@events.Any(o => o is DenormalizerExceptionEvent))
                @events.Add(new DenormalizerExceptionEvent {}.SetEventSource(eventSource));
        }

        
        /// <summary>
        /// write status console
        /// </summary>
        public void WriteStatusConsole(LogSourceType logSourceType, string statusMsg)
            => _appRoot.GetModel<StatusConsoleModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => this.OnError?.Invoke(errorCode, errorMsg));
                await model.WriteConsoleAsync(logSourceType, statusMsg);
             });
        

        /// <summary>
        /// write error status
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="errorMsg"></param>
        public void WriteStatusConsole(LogSourceType logSourceType,int errorCode, string errorMsg)
            => _appRoot.GetModel<StatusConsoleModel>().Execute(async model =>
            {
                model.OnError((errCode, errMsg) => this.OnError?.Invoke(errCode, errMsg));
                await model.WriteConsoleAsync(logSourceType, errorCode, errorMsg);
                OnError?.Invoke(errorCode, errorMsg);
            });
    }
}
