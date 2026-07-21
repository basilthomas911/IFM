namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class UpdateOptionTradeDailyProfitTargetException(string errorMessage) : ApplicationException(errorMessage)
{
}
