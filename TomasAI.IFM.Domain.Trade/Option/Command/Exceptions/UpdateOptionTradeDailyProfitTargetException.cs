namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;

public class UpdateOptionTradeDailyProfitTargetException(string errorMessage) : ApplicationException(errorMessage)
{
}
