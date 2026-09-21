using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public static class InstrumentDefinitionSelectionMapper
{
    public static InstrumentDefinitionSelection Map(Guid snapshot, long index, ExactInstrumentDefinition row)
    {
        DateTimeOffset? Instant(ulong? ns) => ns is { } value && value != ulong.MaxValue
            && value / 100 <= (ulong)(DateTimeOffset.MaxValue.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks)
            ? DateTimeOffset.UnixEpoch.AddTicks((long)(value / 100)) : null;
        var d = row.Summary;
        return new()
        {
            SnapshotId = snapshot, Dataset = row.Dataset, PublisherId = row.PublisherId, InstrumentId = row.InstrumentId,
            RawSymbol = row.RawSymbol, Root = row.Asset, InstrumentClass = row.InstrumentClass,
            Currency = row.Currency, Exchange = row.Exchange, UnderlyingInstrumentId = d.UnderlyingInstrumentId,
            Strike = d.StrikePrice is { } strike ? strike / 1_000_000_000m : null,
            Multiplier = d.ContractMultiplier, TickSize = d.MinimumPriceIncrement is { } tick ? tick / 1_000_000_000m : null,
            ExpirationUtc = Instant(d.ExpirationTimestampNanoseconds), ActivationUtc = Instant(d.ActivationTimestampNanoseconds),
            DefinitionTimestampUtc = Instant(row.EventNanoseconds) ?? throw new InvalidDataException("Invalid definition timestamp."),
            DefinitionDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.Json))).ToLowerInvariant(),
            RawDefinitionReference = FormattableString.Invariant($"instrument_definition:{snapshot:N}:{row.Dataset}:{index % 128}:{index}"),
            Deleted = row.Deleted
        };
    }
}
