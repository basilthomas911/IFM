namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class ChangeOptionTradeLegDataException(string errorMessage) : ApplicationException(errorMessage)
{
}
