namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;

public class ProcessOptionTradeEndOfDayException(string errorMessage) : ApplicationException(errorMessage)
{
}
