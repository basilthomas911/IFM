namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class OptionTradeSnapshotException(string errorMessage) : ApplicationException(errorMessage)
{
}
