using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ServiceApi;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit.Decorators;

/// <summary>
/// A decorator that handles exceptions for trade plan forward loss limit operations and converts them into error
/// events.
/// </summary>
/// <remarks>This class extends <see cref="BaseExceptionDecorator{TState}"/> to provide exception handling
/// specific to the <see cref="TradePlanForwardLossLimitBoundedContextState"/>. It processes exceptions raised during command
/// execution and converts them into appropriate error events for further handling.</remarks>
/// <param name="eventProducer"></param>
/// <param name="logger"></param>
public class TradePlanForwardLossLimitExceptionDecorator(ITradeEventProducer eventProducer, ILogger logger) 
    : BaseExceptionDecorator<TradePlanForwardLossLimitBoundedContextState>(eventProducer, logger)
{
    public override async Task<IErrorEvent> ConvertExceptionToErrorEventAsync(ICommand command, Exception ex)
        => ex switch
        {
            _ => await ConvertCommandExceptionToErrorEventAsync(command, ex)
        };
}

