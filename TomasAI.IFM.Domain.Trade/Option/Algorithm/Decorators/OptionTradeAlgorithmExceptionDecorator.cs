using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeAlgorithm.ServiceApi;
using TomasAI.IFM.Shared.Domain;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.Decorators;

/// <summary>
/// option trade algorithm exception decorator constructor
/// </summary>
/// <param name="eventProducer"></param>
/// <param name="logger"></param>
public class OptionTradeAlgorithmExceptionDecorator(ITradeAlgorithmEventProducer eventProducer, ILogger logger)
    : BaseExceptionDecorator<OptionTradeAlgorithmBoundedContextState>(eventProducer, logger)
{

    /// <summary>
    /// convert command execution exceptions to posted fail events
    /// </summary>
    /// <param name="command"></param>
    /// <param name="ex"></param>
    /// <returns></returns>
    public override async Task<IErrorEvent> ConvertExceptionToErrorEventAsync(ICommand command, Exception ex)
        => ex switch {
            _ => await ConvertCommandExceptionToErrorEventAsync(command, ex)
        };
}

