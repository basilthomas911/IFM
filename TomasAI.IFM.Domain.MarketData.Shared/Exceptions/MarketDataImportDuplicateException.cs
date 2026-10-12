namespace TomasAI.IFM.Domain.MarketData.Shared.Exceptions;

public sealed class MarketDataImportDuplicateException : Exception
{
    public MarketDataImportDuplicateException() { }
    public MarketDataImportDuplicateException(string message, Exception innerException) : base(message, innerException) { }
    public MarketDataImportDuplicateException(string message)
        : base(message)
    {
    }
}
