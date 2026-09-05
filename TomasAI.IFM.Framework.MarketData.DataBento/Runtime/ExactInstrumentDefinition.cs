using System.Globalization;
using System.Text.Json;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>The complete provider JSON record, including fields not projected into ContractDetail.</summary>
public sealed record ExactInstrumentDefinition
{
    public required string Dataset { get; init; }
    public required string Json { get; init; }
    public required ushort PublisherId { get; init; }
    public required uint InstrumentId { get; init; }
    public required string RawSymbol { get; init; }
    public required string Asset { get; init; }
    public required string InstrumentClass { get; init; }
    public required string Currency { get; init; }
    public required string Exchange { get; init; }
    public required ulong ReceivedNanoseconds { get; init; }
    public required ulong EventNanoseconds { get; init; }
    public required bool Deleted { get; init; }
    public required ContractDetail Summary { get; init; }

    public static ExactInstrumentDefinition Parse(string dataset, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var header = root.GetProperty("hd");
        if (header.GetProperty("rtype").GetInt32() != 19) throw new InvalidDataException("Expected a Databento definition record.");
        string Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : "";
        ulong Number(string name) => ulong.Parse(Text(name), CultureInfo.InvariantCulture);
        ulong? Timestamp(string name) => ulong.TryParse(Text(name), out var value) && value != ulong.MaxValue ? value : null;
        var publisher = header.GetProperty("publisher_id").GetUInt16();
        var id = header.GetProperty("instrument_id").GetUInt32();
        var rawSymbol = Text("raw_symbol");
        if (id == 0 || string.IsNullOrWhiteSpace(rawSymbol)) throw new InvalidDataException("Definition identity is missing.");
        var kind = Text("instrument_class") switch { "F" => ContractKind.Future, "C" => ContractKind.CallOption, "P" => ContractKind.PutOption, _ => (ContractKind)0 };
        var expiry = Timestamp("expiration");
        DateOnly? maturity = expiry is { } ns && ns / 1_000_000_000UL <= 253402300799UL
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds((long)(ns / 1_000_000_000UL)).UtcDateTime) : null;
        if (maturity is null && int.TryParse(Text("maturity_year"), out var year) &&
            int.TryParse(Text("maturity_month"), out var month) && int.TryParse(Text("maturity_day"), out var day) &&
            year is >= 1 and <= 9999 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month))
            maturity = new DateOnly(year, month, day);
        var summary = new ContractDetail
        {
            Dataset = dataset, Instrument = new(publisher, id), RawSymbol = rawSymbol, Ticker = Text("asset"),
            Underlying = Text("underlying"), UnderlyingInstrumentId = checked((uint)Number("underlying_id")),
            ContractKind = kind, Currency = Text("currency"), Exchange = Text("exchange"),
            SettlementCurrency = Text("settl_currency"), SecurityType = Text("security_type"), Cfi = Text("cfi"), UnitOfMeasure = Text("unit_of_measure"),
            ActivationTimestampNanoseconds = Timestamp("activation"), ExpirationTimestampNanoseconds = expiry, MaturityDate = maturity
        };
        return new()
        {
            Dataset = dataset, Json = json, PublisherId = publisher, InstrumentId = id, RawSymbol = rawSymbol,
            Asset = summary.Ticker, InstrumentClass = Text("instrument_class"), Currency = summary.Currency, Exchange = summary.Exchange,
            ReceivedNanoseconds = Number("ts_recv"), EventNanoseconds = ulong.Parse(header.GetProperty("ts_event").ToString(), CultureInfo.InvariantCulture),
            Deleted = Text("security_update_action") == "D", Summary = summary
        };
    }
}
