using TomasAI.IFM.Shared.EventModelActor.Contracts;
using System.Globalization;

namespace TomasAI.IFM.Domain.MarketData.Shared;

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
    /// positive invariant decimal, with a fractional part only when needed.</description></item> </list></param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contractId"/> is null, empty, or consists only of whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="contractId"/> is less than 14 characters long or cannot be parsed into its expected
    /// components.</exception>
    public FuturesOptionContractId(string contractId)
    {
        _contractId = contractId;
        if (string.IsNullOrWhiteSpace(contractId))
            throw new ArgumentException("FuturesOptionContractId: contractId is empty");
        if (contractId.Length < 11)
            throw new InvalidOperationException($"FuturesOptionContractId: '{contractId}' is incomplete");
        try
        {
            // Strikes have variable width (the published ES chain includes 10000+).
            // Locate the right marker instead of assuming exactly four strike digits.
            var rightIndex = Math.Max(contractId.LastIndexOf('C'), contractId.LastIndexOf('P'));
            var dateStart = rightIndex - 8;
            if (dateStart < 1 || !DateTime.TryParseExact(contractId.AsSpan(dateStart, 8), "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var maturity)
                || !TryParseStrike(contractId.AsSpan(rightIndex + 1), out var strike))
                throw new FormatException();
            Symbol = contractId.Substring(0, dateStart);
            MaturityDate = maturity;
            OptionType = contractId[rightIndex] == 'P' ? OptionType.Put : OptionType.Call;
            StrikePrice = strike;
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
    public decimal StrikePrice { get; }

    /// <summary>Canonical IDs for new contracts. Existing saved identifiers are never regenerated.</summary>
    public static string Create(string symbol, DateOnly expiry, OptionType right, decimal strike)
    {
        if (string.IsNullOrWhiteSpace(symbol) || symbol.Any(c => !char.IsAsciiLetterOrDigit(c))
            || expiry == DateOnly.MinValue || expiry == DateOnly.MaxValue
            || right is not (OptionType.Call or OptionType.Put) || strike <= 0)
            throw new ArgumentException("A symbol, expiry, right and positive decimal strike are required.");
        return string.Create(CultureInfo.InvariantCulture,
            $"{symbol}{expiry:yyyyMMdd}{(right == OptionType.Call ? 'C' : 'P')}{strike:0.############################}");
    }

    /// <summary>Reads an exact positive invariant strike without rounding over-precision input.</summary>
    public static bool TryParseStrike(ReadOnlySpan<char> value, out decimal strike)
    {
        strike = 0;
        return IsStrikeToken(value)
            && decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out strike)
            && strike > 0 && IsExactStrike(value, strike);
    }

    static bool IsStrikeToken(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || value[0] == '.' || value[^1] == '.') return false;
        var decimalPoint = false;
        foreach (var c in value)
        {
            if (c is >= '0' and <= '9') continue;
            if (c != '.' || decimalPoint) return false;
            decimalPoint = true;
        }
        return true;
    }

    static bool IsExactStrike(ReadOnlySpan<char> token, decimal strike)
    {
        // Decimal.TryParse can round an over-precision input. Never silently change a contract's strike.
        var normalized = token.ToString().TrimStart('0');
        if (normalized.StartsWith('.')) normalized = "0" + normalized;
        if (normalized.Contains('.')) normalized = normalized.TrimEnd('0').TrimEnd('.');
        return normalized == strike.ToString("0.############################", CultureInfo.InvariantCulture);
    }
    public bool IsEmpty => string.IsNullOrEmpty(_contractId);
    public bool IsValid => !IsEmpty && MaturityDate > DateTime.MinValue && StrikePrice > 0;

    public string Format()
        => ContractId ?? "none";

    public override string ToString() => _contractId;
}
