namespace TomasAI.IFM.Domain.MarketData.Shared.Exceptions;

public sealed class MarketDataImportDuplicateException : Exception
{
    public MarketDataImportDuplicateException(string message)
        : base(message)
    {
    }
}
