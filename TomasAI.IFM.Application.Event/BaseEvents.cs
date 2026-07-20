using System;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.Log.Events;

namespace TomasAI.IFM.Application.Event
{
    public abstract class BaseEvents
    {
        private readonly IStatusConsoleServiceApi _statusConsoleLog;
        private int _errorCode;

        public int ErrorCode => _errorCode;


        public BaseEvents(IStatusConsoleServiceApi statusConsoleLog)
        {
            _statusConsoleLog = statusConsoleLog;
        }

        protected async Task ExecuteAsync(int errorCode, Func<Task> taskAction)
        {
            try
            {
                await taskAction();
            }
            catch(Exception ex)
            {
                await WriteConsoleAsync(ex, errorCode);
            }
        }

        protected async Task ExecuteAsync( int errorCode, Action taskAction)
        {
            try
            {
                taskAction();
            }
            catch (Exception ex)
            {
                await WriteConsoleAsync(ex, errorCode);
            }
        }

        protected virtual async Task WriteConsoleAsync(string message) { await Task.CompletedTask; }

        protected async Task WriteConsoleAsync(StatusSourceType statusSourceType, string message)
            => await _statusConsoleLog.StatusConsoleLogUpdatedAsync(new StatusConsoleLoggedEvent
            {
                StatusConsoleLog = new StatusConsoleLogReadModel
                {
                    StatusDate = DateTime.Now,
                    StatusCode = 0,
                    Source = statusSourceType,
                    Message = message
                }
            });

        protected abstract Task WriteConsoleAsync(Exception ex, int errorCode);

        protected async Task WriteConsoleAsync(StatusSourceType statusSourceType, Exception ex, int errorCode)
        {
            _errorCode = errorCode;
            await _statusConsoleLog.StatusConsoleLogUpdatedAsync(new StatusConsoleLoggedEvent
            {
                StatusConsoleLog = new StatusConsoleLogReadModel
                {
                    StatusDate = DateTime.Now,
                    StatusCode = errorCode,
                    Source = statusSourceType,
                    Message = ex.Message,
                    DataType = ex.GetType().Name,
                    Data = ex.ToString()
                }
            });
        }
    }
}
