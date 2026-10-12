namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "The classified provider exception requires its native status or recovery context; generic constructors would lose the failure classification.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "The classified provider exception requires its native status or recovery context; generic constructors would lose the failure classification.")]
public sealed class DatabentoContractMappingException : DatabentoFeedException
{
    /// <summary>Initializes a new DatabentoContractMappingException instance.</summary>
    /// <param name="direction">The mapping direction that failed.</param>
    /// <param name="message">The diagnostic message describing the failure.</param>
    /// <param name="contractId">The futures or option contract identifier.</param>
    /// <param name="instrumentId">The provider instrument identifier.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public DatabentoContractMappingException(
        ContractMappingDirection direction,
        string message,
        string? contractId = null,
        uint? instrumentId = null,
        Exception? innerException = null)
        : base(
            DatabentoFeedStatus.SymbolResolutionFailed,
            message,
            innerException)
    {
        Direction = direction;
        ContractId = contractId;
        InstrumentId = instrumentId;
    }

    public ContractMappingDirection Direction { get; }
    public string? ContractId { get; }
    public uint? InstrumentId { get; }
}
