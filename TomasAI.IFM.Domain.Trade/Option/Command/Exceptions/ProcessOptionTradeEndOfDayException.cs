namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class ProcessOptionTradeEndOfDayException(string errorMessage) : ApplicationException(errorMessage)
{
}
