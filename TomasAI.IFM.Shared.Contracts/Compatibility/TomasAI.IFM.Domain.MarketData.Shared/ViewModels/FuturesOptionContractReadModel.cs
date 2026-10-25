using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;
using System.Globalization;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Represents a futures option contract definition including identifiers, descriptive metadata,
/// financial terms (strike, multiplier), and listing attributes (exchange, currency, security type).
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive / enum fields are serialized. Derived identifiers
/// (<see cref="Id"/>, <see cref="EntityId"/>) are excluded via <see cref="IgnoreMemberAttribute"/>.
/// Utility methods for local symbol generation remain static and are not serialized.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public partial record FuturesOptionContractReadModel
{
    /// <summary>Full contract identifier string (parsed by <c>FuturesOptionContractId</c>).</summary>
    [Key(0)] public string ContractId { get; init; }

    /// <summary>Human-readable contract description.</summary>
    [Key(1)] public string Description { get; init; }

    /// <summary>Underlying futures contract root symbol (e.g. ES, NQ).</summary>
    [Key(2)] public string Symbol { get; init; }

    /// <summary>Generated local option symbol (exchange specific short code).</summary>
    [Key(3)] public string LocalSymbol { get; init; }

    /// <summary>Security type (e.g. FUT, OPT).</summary>
    [Key(4)] public string SecurityType { get; init; }

    /// <summary>Trading currency.</summary>
    [Key(5)] public string Currency { get; init; }

    /// <summary>Primary listing / execution exchange.</summary>
    [Key(6)] public string Exchange { get; init; }

    /// <summary>Contract multiplier (volume to notional conversion factor).</summary>
    [Key(7)] public string Multiplier { get; init; }

    /// <summary>Contract month (year/month used for expiry grouping).</summary>
    [Key(8)] public DateOnly ContractMonth { get; init; }

    /// <summary>Option strike price.</summary>
    [Key(9)] public double StrikePrice { get; init; }

    /// <summary>Option type code (e.g. "Call", "Put").</summary>
    [Key(10)] public string OptionType { get; init; }

    /// <summary>Strongly typed option contract identifier (not serialized).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesOptionContractId Id => string.IsNullOrEmpty(ContractId) ? new() : new(ContractId);

    /// <summary>Entity identifier grouped by contract year (not serialized).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesOptionContractEntityId EntityId => new(ContractId ?? string.Empty, ContractMonth.Year);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesOptionContractReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized properties.
    /// </summary>
    public FuturesOptionContractReadModel(
        string contractId,
        string description,
        string symbol,
        string localSymbol,
        string securityType,
        string currency,
        string exchange,
        string multiplier,
        DateOnly contractMonth,
        double strikePrice,
        string optionType)
    {
        ContractId = contractId;
        Description = description;
        Symbol = symbol;
        LocalSymbol = localSymbol;
        SecurityType = securityType;
        Currency = currency;
        Exchange = exchange;
        Multiplier = multiplier;
        ContractMonth = contractMonth;
        StrikePrice = strikePrice;
        OptionType = optionType;

    }

    /// <summary>
    /// Generates a combined local symbol with abbreviated option type and zero-padded strike.
    /// </summary>
    /// <param name="localSymbol">Base local symbol.</param>
    /// <param name="optionType">Option type string ("Call"/"Put").</param>
    /// <param name="strikePrice">Strike price.</param>
    /// <returns>Formatted local symbol with type and strike.</returns>
    public static string GetContractLocalSymbol(string localSymbol, string optionType, double strikePrice)
        
    {
        ArgumentNullException.ThrowIfNull(optionType);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{localSymbol} {optionType.AsSpan(0, 1)}{strikePrice:0000.################}");
    }

    public static string GetExactContractLocalSymbol(string localSymbol, string optionType, decimal strikePrice)
        
    {
        ArgumentNullException.ThrowIfNull(optionType);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{localSymbol} {optionType.AsSpan(0, 1)}{strikePrice:0000.############################}");
    }

    /// <summary>
    /// Builds a local symbol based on underlying symbol, week number within month, contract month code and year digit.
    /// </summary>
    /// <param name="symbol">Underlying futures symbol.</param>
    /// <param name="valueDate">Date used to compute week and month codes.</param>
    /// <returns>Exchange-compatible local symbol.</returns>
    public static string GetLocalSymbol(string symbol, DateOnly valueDate)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        string weekSymbol;
        var weekNumber = GetWeekNumber();
        if (weekNumber == 3 && IsContractMonth())
            weekSymbol = "S";
        else if (weekNumber > 4 || IsLastDayOfMonth())
            weekSymbol = "W";
        else
            weekSymbol = valueDate.DayOfWeek switch
            {
                DayOfWeek.Monday => $"{weekNumber}A",
                DayOfWeek.Wednesday => $"{weekNumber}C",
                _ => $"W{weekNumber}"
            };

        var assetSymbol = symbol.Substring(0, 1);
        var monthSymbol = new[] { "F", "G", "H", "J", "K", "M", "N", "Q", "U", "V", "X", "Z" }[valueDate.Month - 1];
        var yearSymbol = $"{valueDate.Year}".Substring(3, 1);
        return $"{assetSymbol}{weekSymbol}{monthSymbol}{yearSymbol}";

        int GetWeekNumber()
        {
            var startDate = new DateOnly(valueDate.Year, valueDate.Month, 1);
            var numWDays = valueDate.DayNumber - startDate.DayNumber;
            return (numWDays / 7) + 1;
        }

        bool IsContractMonth() => valueDate.Month is 3 or 6 or 9 or 12;

        bool IsLastDayOfMonth()
        {
            var nextMonth = valueDate.AddMonths(1);
            var firstDayNext = new DateOnly(nextMonth.Year, nextMonth.Month, 1);
            return firstDayNext.AddDays(-1).Equals(valueDate) || valueDate.DayOfWeek == DayOfWeek.Thursday;
        }
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [IgnoreMember]
    public bool IsValid
        => !string.IsNullOrEmpty(ContractId) &&
           !string.IsNullOrEmpty(Symbol) &&
           !string.IsNullOrEmpty(LocalSymbol) &&
           !string.IsNullOrEmpty(SecurityType) &&
           !string.IsNullOrEmpty(Currency) &&
           !string.IsNullOrEmpty(Exchange) &&
           !string.IsNullOrEmpty(Multiplier) && ContractMonth != default && StrikePrice > 0 &&
           !string.IsNullOrEmpty(OptionType);
}

/// <summary>The key-9 double is retained only for wire compatibility; new reference flows use StrikePriceDecimal.</summary>
public partial record FuturesOptionContractReadModel
{
    [Key(11)] public decimal? StrikePriceDecimal { get; init; }
    [Key(12)] public int SchemaVersion { get; init; }
    [Key(13)] public ReferenceReviewState ReviewState { get; init; }
    [Key(14)] public string? Dataset { get; init; }
    [Key(15)] public ushort? PublisherId { get; init; }
    [Key(16)] public uint? InstrumentId { get; init; }
    [Key(17)] public string? RawSymbol { get; init; }
    [Key(18)] public DateTimeOffset? DefinitionTimestampUtc { get; init; }
    [Key(19)] public string? DefinitionDigest { get; init; }
    [Key(20)] public string? RawDefinitionReference { get; init; }
    [Key(21)] public DateTimeOffset? ExpirationUtc { get; init; }
    [Key(22)] public DateTimeOffset? LastTradingUtc { get; init; }
    [Key(23)] public string? ExchangeTimeZoneId { get; init; }
    [Key(24)] public ReferenceSettlementStyle SettlementStyle { get; init; }
    [Key(25)] public decimal? MultiplierValue { get; init; }
    [Key(26)] public decimal? PriceScale { get; init; }
    [Key(27)] public decimal? TickSize { get; init; }
    [Key(28)] public string? CalendarVersion { get; init; }
    [Key(29)] public string? MappingVersion { get; init; }
    [Key(30)] public string? EvidenceId { get; init; }
    [Key(31)] public DateTimeOffset? EffectiveFromUtc { get; init; }
    [Key(32)] public DateTimeOffset? EffectiveUntilUtc { get; init; }
    [Key(33)] public string? UnderlyingContractId { get; init; }
    [Key(34)] public ReferenceAssetType UnderlyingAssetType { get; init; }
    [Key(35)] public ReferenceOptionRight OptionRight { get; init; }
    [Key(36)] public ReferenceExerciseStyle ExerciseStyle { get; init; }
    [Key(37)] public ReferencePremiumStyle PremiumStyle { get; init; }
    [Key(38)] public DateTimeOffset? ExerciseCutoffUtc { get; init; }
    [Key(39)] public string? ExerciseResultContractId { get; init; }
    [Key(40)] public ReferencePremiumTickRule PremiumTickRule { get; init; }
    [Key(41)] public string? TickRuleVersion { get; init; }
    [Key(42)] public ReferenceDayCount DayCount { get; init; }
    [Key(43)] public uint? UnderlyingInstrumentId { get; init; }
    [Key(44)] public ushort? UnderlyingPublisherId { get; init; }

    /// <summary>Explicit legacy translation; conflicting or invalid representations fail rather than round silently.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Exact equality verifies lossless decimal/double representation or a zero-width denominator sentinel; epsilon comparison would change validation.")]
    public decimal GetExactStrikePrice()
    {
        if (!double.IsFinite(StrikePrice) || StrikePrice <= 0) throw new InvalidDataException("Invalid legacy strike.");
        if (StrikePriceDecimal is { } exact)
        {
            if (exact <= 0 || (double)exact != StrikePrice) throw new InvalidDataException("Decimal and legacy strike conflict.");
            return exact;
        }
        if (!decimal.TryParse(StrikePrice.ToString("R", CultureInfo.InvariantCulture),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var translated) || translated <= 0
            || (double)translated != StrikePrice)
            throw new InvalidDataException("Legacy strike cannot be translated without changing its value.");
        return translated;
    }
}
