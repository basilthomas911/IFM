namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;

public class ChangeOptionTradeLegDataException(string errorMessage) : ApplicationException(errorMessage)
{
}
