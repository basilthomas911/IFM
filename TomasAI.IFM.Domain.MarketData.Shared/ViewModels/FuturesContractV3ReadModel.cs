using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;
using System.Globalization;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Represents a futures contract with detailed information, including contract identifiers, trading details, and
/// metadata.
/// </summary>
/// <remarks>This view model is used to encapsulate the key attributes of a futures contract, such as its unique
/// identifiers, trading symbol, security type, currency, exchange, and other relevant details. It also provides a
/// derived property to generate a composite identifier for the contract.</remarks>
[MessagePackObject(AllowPrivate = true)]
public partial record FuturesContractV3ReadModel
{
    [Key(0)]
    public string ContractId { get; init; }
    [Key(1)]
    public string Description { get; init; }
    [Key(2)]
    public string Symbol { get; init; }
    [Key(3)]
    public string LocalSymbol { get; init; }
    [Key(4)]
    public string SecurityType { get; init; }
    [Key(5)]
    public string Currency { get; init; }
    [Key(6)]
    public string Exchange { get; init; }
    [Key(7)]
    public string Multiplier { get; init; }
    [Key(8)]
    public DateOnly LastTradeDate { get; init; }
    [Key(9)]
    public bool OnTheRun { get; init; }
    [Key(10)]
    public bool Rollover { get; init; }

    [JsonIgnore]
    [IgnoreMember]
    public FuturesContractId Id => new(ContractId, Symbol, LastTradeDate);

    public FuturesContractV3ReadModel()
    {
        ContractId = default!;
        Symbol = default!;
        LastTradeDate = DateOnly.MinValue;
    }

    public FuturesContractV3ReadModel(
        string contractId,
        string description,
        string symbol,
        string localSymbol,
        string securityType,
        string currency,
        string exchange,
        string multiplier,
        DateOnly lastTradeDate,
        bool onTheRun)
        : this(
            contractId,
            description,
            symbol,
            localSymbol,
            securityType,
            currency,
            exchange,
            multiplier,
            lastTradeDate,
            onTheRun,
            onTheRun)
    {
    }

    [SerializationConstructor]
    public FuturesContractV3ReadModel(
        string contractId,
        string description,
        string symbol,
        string localSymbol,
        string securityType,
        string currency,
        string exchange,
        string multiplier,
        DateOnly lastTradeDate,
        bool onTheRun,
        bool rollover)
    {
        ContractId = contractId;
        Description = description;
        Symbol = symbol;
        LocalSymbol = localSymbol;
        SecurityType = securityType;
        Currency = currency;
        Exchange = exchange;
        Multiplier = multiplier;
        LastTradeDate = lastTradeDate;
        OnTheRun = onTheRun;
        Rollover = rollover;
    }

    [IgnoreMember]
    public bool IsValid
        => !string.IsNullOrEmpty(ContractId)
            && !string.IsNullOrEmpty(Symbol)
            && LastTradeDate > DateOnly.MinValue
            && (!OnTheRun || Rollover);
}

/// <summary>Append-only provider/review evidence. Missing legacy fields never imply qualification.</summary>
public partial record FuturesContractV3ReadModel
{
    [Key(11)] public int SchemaVersion { get; init; }
    [Key(12)] public ReferenceReviewState ReviewState { get; init; }
    [Key(13)] public string? Dataset { get; init; }
    [Key(14)] public ushort? PublisherId { get; init; }
    [Key(15)] public uint? InstrumentId { get; init; }
    [Key(16)] public string? RawSymbol { get; init; }
    [Key(17)] public DateTimeOffset? DefinitionTimestampUtc { get; init; }
    [Key(18)] public string? DefinitionDigest { get; init; }
    [Key(19)] public string? RawDefinitionReference { get; init; }
    [Key(20)] public DateTimeOffset? ExpirationUtc { get; init; }
    [Key(21)] public DateTimeOffset? LastTradingUtc { get; init; }
    [Key(22)] public string? ExchangeTimeZoneId { get; init; }
    [Key(23)] public ReferenceSettlementStyle SettlementStyle { get; init; }
    [Key(24)] public decimal? MultiplierValue { get; init; }
    [Key(25)] public decimal? PriceScale { get; init; }
    [Key(26)] public decimal? TickSize { get; init; }
    [Key(27)] public string? CalendarVersion { get; init; }
    [Key(28)] public string? MappingVersion { get; init; }
    [Key(29)] public string? EvidenceId { get; init; }
    [Key(30)] public DateTimeOffset? EffectiveFromUtc { get; init; }
    [Key(31)] public DateTimeOffset? EffectiveUntilUtc { get; init; }
}
