using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ServiceApi;

namespace TomasAI.IFM.Domain.Trade.Plan.Decorators;

public class TradePlanExceptionDecorator(ITradeEventProducer eventProducer, ILogger logger) : BaseExceptionDecorator<TradePlanBoundedContextState>(eventProducer, logger)
{
    /// <summary>
    /// convert command execution exceptions to posted fail events
    /// </summary>
    /// <param name="command"></param>
    /// <param name="ex"></param>
    public override async Task<IErrorEvent> ConvertExceptionToErrorEventAsync(ICommand command, Exception ex)
        => ex switch
        {
            _ => await ConvertCommandExceptionToErrorEventAsync(command, ex)
        };
}

