namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;

public class OptionTradeSnapshotException(string errorMessage) : ApplicationException(errorMessage)
{
}
