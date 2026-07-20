using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.Events;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ReadModels;

namespace TomasAI.IFM.Service.ErrorConsole
{
    public class ErrorConsoleService : IErrorConsoleService
    {
        private readonly IErrorConsoleEventProducer _errorConsole;

        public ErrorConsoleService(IErrorConsoleEventProducer errorConsoleServiceApi)
        {
            _errorConsole = errorConsoleServiceApi;
        }

        public async Task ExecuteAsync(CommandExceptionEvent e)
            =>  await _errorConsole.PostEventAsync(new ErrorConsoleLoggedEvent {
                CommandId = e.CommandId,
                ErrorConsoleLog = new ErrorConsoleLogReadModel
                {
                    LogSource = LogSourceType.Command,
                    ErrorDate = e.ErrorDate,
                    ErrorCode = e.ErrorCode,
                    ErrorMessage = e.ErrorMessage,
                    ErrorData = e.ErrorData,
                    ExtendedErrorData = e.CommandData
                }
            });
        
        public async Task ExecuteAsync(QueryExceptionEvent e) 
            => await _errorConsole.PostEventAsync(new ErrorConsoleLoggedEvent  {
                CommandId = e.CommandId,
                ErrorConsoleLog = new ErrorConsoleLogReadModel
                {
                    LogSource = LogSourceType.Query,
                    ErrorDate = e.ErrorDate,
                    ErrorCode = e.ErrorCode,
                    ErrorMessage = e.ErrorMessage,
                    ErrorData = e.ErrorData,
                }
            });

        public async Task ExecuteAsync(EventServiceExceptionEvent e) 
            => await _errorConsole.PostEventAsync(new ErrorConsoleLoggedEvent {
                CommandId = e.CommandId,
                ErrorConsoleLog = new ErrorConsoleLogReadModel
                {
                    LogSource = LogSourceType.Event,
                    ErrorDate = e.ErrorDate,
                    ErrorCode = e.ErrorCode,
                    ErrorMessage = e.ErrorMessage,
                    ErrorData = e.ErrorData,
                    ExtendedErrorData = e.EventData
                }
            });

        
    }
}
