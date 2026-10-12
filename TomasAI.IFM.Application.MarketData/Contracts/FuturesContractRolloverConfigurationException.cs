namespace TomasAI.IFM.Application.MarketData.Contracts;

public sealed class FuturesContractRolloverConfigurationException : MarketDataApiException
{
    /// <summary>Creates the exception with the supplied diagnostic cause.</summary>
    public FuturesContractRolloverConfigurationException() : base("FuturesContractRolloverConfigurationException") { }
    /// <summary>Creates the exception with the supplied diagnostic cause.</summary>
    public FuturesContractRolloverConfigurationException(string message, Exception innerException) : base(message, innerException) { }
    public FuturesContractRolloverConfigurationException(string message)
        : base(message)
    {
    }
}
