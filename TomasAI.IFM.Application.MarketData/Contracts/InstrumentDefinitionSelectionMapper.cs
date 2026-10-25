using TomasAI.IFM.Shared.MarketData.Definitions;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public static class InstrumentDefinitionSelectionMapper
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1308", Justification = "Lowercase digest text is a persisted content identity, not a case-insensitive comparison key.")]
    public static InstrumentDefinitionSelection Map(Guid snapshot, long index, ExactInstrumentDefinition row)
    {
        ArgumentNullException.ThrowIfNull(row);
        DateTimeOffset? Instant(ulong? ns) => ns is { } value && value != ulong.MaxValue
            && value / 100 <= (ulong)(DateTimeOffset.MaxValue.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks)
            ? DateTimeOffset.UnixEpoch.AddTicks((long)(value / 100)) : null;
        var d = row.Summary;
        return new()
        {
            SnapshotId = snapshot,
            Dataset = row.Dataset,
            PublisherId = row.PublisherId,
            InstrumentId = row.InstrumentId,
            RawSymbol = row.RawSymbol,
            Root = row.Asset,
            InstrumentClass = row.InstrumentClass,
            Currency = row.Currency,
            Exchange = row.Exchange,
            UnderlyingInstrumentId = d.UnderlyingInstrumentId,
            Strike = d.StrikePrice is { } strike ? strike / 1_000_000_000m : null,
            Multiplier = PricePointMultiplier(row),
            TickSize = d.MinimumPriceIncrement is { } tick ? tick / 1_000_000_000m : null,
            ExpirationUtc = Instant(d.ExpirationTimestampNanoseconds),
            ActivationUtc = Instant(d.ActivationTimestampNanoseconds),
            DefinitionTimestampUtc = Instant(row.EventNanoseconds) ?? throw new InvalidDataException("Invalid definition timestamp."),
            DefinitionDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.Json))).ToLowerInvariant(),
            RawDefinitionReference = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"instrument_definition:{snapshot:N}:{row.Dataset}:{index % 128}:{index}"),
            Deleted = row.Deleted
        };
    }
    /// <summary>Maps ES USD/index-point economics only when quantity and venue tick value agree. Deliverable count is not a monetary multiplier.</summary>
    public static decimal? PricePointMultiplier(ExactInstrumentDefinition row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if(row.Asset!="ES"||row.Currency!="USD"||row.Summary.UnitOfMeasure!="IPNT")return null;
        using var json=System.Text.Json.JsonDocument.Parse(row.Json);
        decimal? Fixed(string name)=>json.RootElement.TryGetProperty(name,out var value)
            && long.TryParse(value.ToString(),System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var integer)
            && integer!=long.MaxValue ? integer/1_000_000_000m : null;
        var quantity=Fixed("unit_of_measure_qty");var display=Fixed("display_factor");
        var tick=Fixed("min_price_increment");var tickValue=Fixed("min_price_increment_amount");
        if(quantity is not >0||display is not >0)return null;
        // ES options with a variable tick rule have no single tick increment/value.
        // Their USD/index-point contract quantity is still an exact provider fact.
        if(row.Summary.ContractKind is ContractKind.CallOption or ContractKind.PutOption
            && (tick is not >0||tickValue is not >0))return quantity;
        if(tick is not >0||tickValue is not >0)return null;
        return tickValue.Value/display.Value/tick.Value==quantity.Value?quantity:null;
    }

}
