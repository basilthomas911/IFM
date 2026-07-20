using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.LogDb;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Log.ViewModels;
using QLNet;

namespace TomasAI.IFM.Application.Event.Denormalizer
{
    public class ExceptionEventDenormalizer : BaseEventDenormalizer,
        IAsyncEventHandler<CommandExceptionEvent>,
        IAsyncEventHandler<QueryExceptionEvent>,
        IAsyncEventHandler<EventServiceExceptionEvent>,
        IAsyncEventHandler<DenormalizerExceptionEvent>
    {
        private readonly ILogDbContext _dbLog;

        public ExceptionEventDenormalizer(ILogDbContext dbLog, ILogger<ExceptionEventDenormalizer> logger):base(logger)
        {
            _dbLog = dbLog;
        }

        /// <summary>
        /// log command exceptions
        /// </summary>
        /// <param name="e">command exception event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(CommandExceptionEvent e)
            => await base.ExecuteAsync(() => _dbLog.InsertCommandLogAsync(new CommandLogReadModel(
                    commandId: e.CommandId.ToString(),
                    commandDate: e.ErrorDate,
                    commandName: e.CommandName,
                    aggregateId: e.AggregateId,
                    routeTo: e.RouteTo,
                    commandData: e.CommandData,
                    userName: e.UserName,
                    errorMessage: e.ErrorMessage,
                    errorCode: e.ErrorCode,
                    errorType: e.ErrorType,
                    errorData: e.ErrorData)));

        /// <summary>
        /// log query exceptions
        /// </summary>
        /// <param name="e">query exception event</param>
        /// <returns></returns>
        public async Task ExecuteAsync(QueryExceptionEvent e)
            => await base.ExecuteAsync(() => _dbLog.InsertQueryLogAsync(new QueryLogViewModel(
                    commandId: e.CommandId.ToString(),
                    queryDate: e.ErrorDate,
                    queryName: e.QueryName,
                    queryData: e.QueryData,
                    userName: e.UserName,
                    errorMessage: e.ErrorMessage,
                    errorCode: e.ErrorCode,
                    errorType: e.ErrorType,
                    errorData: e.ErrorData)));

        /// <summary>
        /// log event service exceptions
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(EventServiceExceptionEvent e)
            => await base.ExecuteAsync(() => _dbLog.InsertEventServiceLogAsync(new EventServiceLogViewModel(
                    commandId: e.CommandId.ToString(),
                    eventDate: e.ErrorDate,
                    eventName: e.EventName,
                    eventData: e.EventData,
                    userName: e.UserName,
                    errorMessage: e.ErrorMessage,
                    errorCode: e.ErrorCode,
                    errorType: e.ErrorType,
                    errorData: e.ErrorData)));

        /// <summary>
        /// log denormalizer exceptions
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(DenormalizerExceptionEvent e) 
            => await base.ExecuteAsync(() => _dbLog.InsertDenormalizerLogAsync(new DenormalizerLogViewModel(
                     commandId: e.CommandId.ToString(),
                     denormalizerDate: e.ErrorDate,
                     denormalizerName: e.DenormalizerName,
                     denormalizerData: e.DenormalizerData,
                     userName: e.UserName,
                     errorMessage: e.ErrorMessage,
                     errorCode: e.ErrorCode,
                     errorType: e.ErrorType,
                     errorData: e.ErrorData)));

    }
}
