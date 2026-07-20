using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Event.Denormalizer
{
    public abstract class BaseEventDenormalizer
    {
        private Func<IEvent, Task> _fireEventAsync;
        private readonly ILogger _logger;

        public BaseEventDenormalizer(ILogger logger)
        {
            _logger = logger;
        }

        protected  void SetEventProducer(Func<IEvent, Task> eventProducerAction) => _fireEventAsync = eventProducerAction;

        /// <summary>
        /// execute denormalizer action
        /// </summary>
        /// <param name="e"></param>
        /// <param name="denormalizerAction"></param>
        /// <returns></returns>
        protected async Task DenormalizeAsync(IEvent e, int errorCode, Func<Task> denormalizerAction)
        {
            try
            {
                e.CheckForEmptyCommandId();
                await denormalizerAction();
                var completeEvent = e.ToCompletedEvent();
                await _fireEventAsync(completeEvent);
                var denormalizerCompleteEvent = ToDenormalizerCompletedEvent(completeEvent);
                await _fireEventAsync(denormalizerCompleteEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DenormalizeAsync Failed");
                e.CommandId = e.CommandId == Guid.Empty ? Guid.NewGuid() : e.CommandId;
                var failEvent = e.ToFailedEvent(ex);
                await _fireEventAsync(failEvent);
                var denormalizerExceptionEvent = ToDenormalizerExceptionEvent(e, ex, errorCode);
                await _fireEventAsync(denormalizerExceptionEvent);
            }
        }

        /// <summary>
        /// execute denormalizer action with no error checking 
        /// </summary>
        /// <param name="e"></param>
        /// <param name="denormalizerAction"></param>
        /// <remarks>used for denormalizing exception events</remarks>
        /// <returns></returns>
        protected async Task ExecuteAsync(Func<Task> denormalizerAction)
        {
            try
            {
                await denormalizerAction();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Denormalizer.ExecuteAsync: Failed");
            }
        }

        protected DenormalizerCompletedEvent ToDenormalizerCompletedEvent(ICompleteEvent e)
            => new DenormalizerCompletedEvent {
                CommandId = e.CommandId,
                CreatedOn = DateTime.Now,
                CreatedBy = $"{Environment.UserDomainName}\\{Environment.UserName}"
            };

        protected DenormalizerExceptionEvent ToDenormalizerExceptionEvent(DomainEvent e, Exception ex, int errorCode)
            => new DenormalizerExceptionEvent {
                CommandId = e.CommandId,
                DenormalizerName = e.GetType().Name,
                DenormalizerData = JsonConvert.SerializeObject(e, Formatting.Indented),
                ErrorMessage = ex.Message,
                ErrorType = ErrorType.Denormalizer,
                ErrorData = ex.ToString(),
                ErrorCode = errorCode
            };

    }

}
