namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

public sealed class FmpMarketDataImportException : Exception
{
    /// <summary>Creates the exception with the supplied diagnostic cause.</summary>
    public FmpMarketDataImportException() : base("FmpMarketDataImportException") { }
    /// <summary>Creates the exception with the supplied diagnostic cause.</summary>
    public FmpMarketDataImportException(string message, Exception innerException) : base(message, innerException) { }
    public FmpMarketDataImportException(string message)
        : base(message)
    {
    }
}
