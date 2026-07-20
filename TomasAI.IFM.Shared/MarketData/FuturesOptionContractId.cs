using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.MarketData;

/// <summary>
/// Represents the unique identifier for a futures option contract, encapsulating details such as the contract symbol,
/// maturity date, option type, and strike price.
/// </summary>
/// <remarks>This class provides a structured representation of a futures option contract identifier, which is
/// typically parsed from a string format. The identifier includes the contract symbol, maturity date, option type (put
/// or call), and strike price. Instances of this class can be used to validate, format, and retrieve these components
/// in a strongly-typed manner.</remarks>
public class FuturesOptionContractId: IActorEntityId
{
     readonly string _contractId;

    public FuturesOptionContractId()
    {
        _contractId = string.Empty;
        Symbol = string.Empty;
        MaturityDate = DateTime.MinValue;
        OptionType = OptionType.Put;
        StrikePrice = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesOptionContractId"/> class using the specified contract
    /// identifier.
    /// </summary>
    /// <remarks>This constructor parses the provided <paramref name="contractId"/> to extract the contract
    /// symbol, maturity date, option type, and strike price. If the format is invalid or parsing fails, an exception is
    /// thrown.</remarks>
    /// <param name="contractId">The contract identifier string representing the futures option contract.  The string must be at least 14
    /// characters long and follow the format:  [Symbol][YYYYMMDD][OptionType][StrikePrice], where: <list type="bullet">
    /// <item><description><c>Symbol</c>: The contract symbol (e.g., "ES").</description></item>
    /// <item><description><c>YYYYMMDD</c>: The maturity date in year, month, and day format.</description></item>
    /// <item><description><c>OptionType</c>: A single character indicating the option type ('P' for put, 'C' for
    /// call).</description></item> <item><description><c>StrikePrice</c>: The strike price as an
    /// integer.</description></item> </list></param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contractId"/> is null, empty, or consists only of whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="contractId"/> is less than 14 characters long or cannot be parsed into its expected
    /// components.</exception>
    public FuturesOptionContractId(string contractId)
    {
        _contractId = contractId;
        if (string.IsNullOrWhiteSpace(contractId))
            throw new ArgumentException("FuturesOptionContractId: contractId is empty");
        if (contractId.Length < 14)
            throw new InvalidOperationException($"FuturesOptionContractId: '{contractId}' length is less than 14");
        try
        {
            var dateStart = contractId.Length - 13;
            Symbol = contractId.Substring(0, dateStart);
            var year = Convert.ToInt32(contractId.Substring(dateStart, 4));
            var month = Convert.ToInt32(contractId.Substring(dateStart + 4, 2));
            var day = Convert.ToInt32(contractId.Substring(dateStart + 6, 2));
            MaturityDate = new DateTime(year, month, day);
            var optionType = contractId.Substring(dateStart + 8, 1);
            OptionType = optionType == "P" ? OptionType.Put : OptionType.Call;
            StrikePrice = Convert.ToInt32(contractId[(dateStart + 9)..]);
        }
        catch
        {
            throw new InvalidOperationException($"FuturesOptionContractId: unable to parse '{contractId}'");
        }
    }

    public string ContractId => _contractId;
    public string Symbol { get; }
    public DateTime MaturityDate { get; }
    public OptionType OptionType { get; }
    public int StrikePrice { get; }
    public bool IsEmpty => string.IsNullOrEmpty(_contractId);
    public bool IsValid => !IsEmpty && MaturityDate > DateTime.MinValue && StrikePrice > 0;

    public string Format()
        => ContractId ?? "none";

    public override string ToString() => _contractId;
}
