using System.Runtime.InteropServices;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>One copied source record and the derived state captured under its
/// native writer. No later hot-cache read is used to build these snapshots.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct LivePublication
{
    /// <summary>Exact source record and generation identity.</summary>
    public LiveMarketEvent Source;
    /// <summary>656-byte DTO identity.</summary>
    public uint Size;
    /// <summary>Presence: market=1, quote=2, trade=4, session=8, VWAP=16, option statistics=32.</summary>
    public uint Flags;
    /// <summary>0 unchanged, 1 quote, 2 suppressed trade, 3 published trade, 4 statistics, 5 trade replay completion, 6 statistics replay completion.</summary>
    public uint Update;
    /// <summary>Reserved; always zero.</summary>
    public uint Reserved;
    /// <summary>Exact fixed slots; see ABI-OWNERSHIP.md.</summary>
    public fixed long Fields[64];
    /// <summary>API observation time captured at native source processing.
    /// Defined for queued publications; non-consuming state views leave it zero.</summary>
    public DateTimeOffset ObservedAtUtc {get{fixed(long* f=Fields)return Timestamp(f[63]);}}

    static DateOnly Date(long value) => new(checked((int)(value / 10_000)), checked((int)(value / 100 % 100)), checked((int)(value % 100)));
    static DateTimeOffset Timestamp(long ticks) => DateTimeOffset.UnixEpoch.AddTicks(ticks);
    static Guid Epoch(long low, long high)
    {
        Span<byte> bytes = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes, low);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes[8..], high);
        return new(bytes);
    }
    /// <summary>Maps the exact captured raw quote, including provider order
    /// counts and local ingestion time, without acquiring a subscription.</summary>
    public bool TryGetLastQuote(string contractId, DateOnly valueDate,
        out TomasAI.IFM.Shared.MarketData.Contracts.LastPrice.LastQuoteTickSnapshot snapshot)
    {
        if ((Flags & 2) == 0) { snapshot = default; return false; }
        fixed (long* f = Fields)
        {
            if(Date(f[0])!=valueDate)throw new InvalidDataException("Native quote belongs to another API ValueDate.");
            snapshot=new(contractId,valueDate,f[5]!=0?f[6]/1_000_000_000m:null,checked((uint)f[7]),checked((uint)f[8]),
                f[9]!=0?f[10]/1_000_000_000m:null,checked((uint)f[11]),checked((uint)f[12]),f[1],Timestamp(f[2]),Timestamp(f[3]))
                {LocalReceivedAtUtc=Timestamp(f[4])};
        }
        return true;
    }
    /// <summary>Last provider statistics time, independent of saved statistics
    /// reference dates; unknown remains absent rather than a zero timestamp.</summary>
    public DateTimeOffset? OptionStatisticsAtUtc
    {
        get { fixed(long* f=Fields)return (Flags&32)!=0&&(f[52]&4)!=0?Timestamp(f[55]):null; }
    }
    /// <summary>Maps the existing statistics contract without rounding or date inference.</summary>
    public bool TryGetSessionStatistics(string contractId, out FuturesSessionStatisticsSnapshot snapshot)
    {
        if ((Flags & 8) == 0) { snapshot = default; return false; }
        fixed (long* f = Fields)
            snapshot = new(contractId, Date(f[25]), f[26] / 1_000_000_000m, f[27] / 1_000_000_000m,
                f[28] / 1_000_000_000m, checked((uint)f[29]), f[30], f[31], (FuturesSessionVolumeQuality)f[32]);
        return true;
    }
    /// <summary>Maps nullable saved/observed/official option volume and open
    /// interest with their independent exchange reference dates. Unknown is null.</summary>
    public bool TryGetOptionStatistics(string contractId,out TomasAI.IFM.Shared.MarketData.Contracts.OptionChainStatisticsSnapshot? snapshot){
        if((Flags&32)==0){snapshot=null;return false;}
        fixed(long* f=Fields){
            var mask=f[52];if(mask<0||mask>511||f[62]<0||f[62]>uint.MaxValue)
                throw new InvalidDataException("Malformed native option statistics.");
            snapshot=new(contractId,(mask&1)!=0?f[53]:null,(mask&8)!=0?Date(f[56]):null,
                (mask&2)!=0?f[54]:null,(mask&16)!=0?Date(f[57]):null);return true;
        }
    }
    /// <summary>Maps the existing market-price contract from this record's
    /// captured state. Accepted same-price trades retain publication continuity.</summary>
    public bool TryGetMarketPrice(string contractId, AssetTypeId assetType, out FuturesMarketPriceSnapshot snapshot)
    {
        if ((Flags & 1) == 0) { snapshot = default; return false; }
        fixed (long* f = Fields)
        {
            FuturesMarketQuoteSnapshot? quote = (Flags & 2) == 0 ? null : new(
                f[5] != 0 ? f[6] / 1_000_000_000m : null, checked((uint)f[7]),
                f[9] != 0 ? f[10] / 1_000_000_000m : null, checked((uint)f[11]),
                checked((uint)f[8]), checked((uint)f[12]), f[1], Timestamp(f[2]), Timestamp(f[3]));
            FuturesMarketTradeSnapshot? trade = (Flags & 4) == 0 ? null : new(
                f[17] / 1_000_000_000m, checked((uint)f[18]), f[14], Timestamp(f[15]), Timestamp(f[16]),
                (NormalizedTradeAction)f[19], (NormalizedTradeSide)f[20], (NormalizedTradeConditionFlags)f[21], Epoch(f[22], f[23]), f[24]);
            snapshot = new(contractId, Source.Instrument, checked((ushort)(Source.Fields[4] & 0xffff)), assetType, Date(f[0]), quote, trade);
        }
        return true;
    }
    static decimal ExactDecimal(long low, long high, long scale)
    {
        if ((ulong)high > uint.MaxValue || scale is < 0 or > 9) throw new InvalidDataException("Invalid native decimal coefficient/scale.");
        var bits = unchecked((ulong)low);
        return new(unchecked((int)bits), unchecked((int)(bits >> 32)), unchecked((int)high), false, (byte)scale);
    }
    /// <summary>Maps the complete source checkpoint, reconstructing CLR decimal
    /// coefficient/scale directly. Availability and validity remain separate.</summary>
    public bool TryGetVwap(out FuturesVwapSourceCheckpoint? checkpoint)
    {
        if ((Flags & 16) == 0) { checkpoint = null; return false; }
        fixed (long* f = Fields)
            checkpoint = new()
            {
                StreamEpochId = Epoch(f[33], f[34]), LastTradeOrdinal = f[35], LastTradeSourceSequence = f[36], AsOfUtc = Timestamp(f[37]),
                CumulativePriceVolume = ExactDecimal(f[38], f[39], f[40]), CumulativeVolume = f[41], EligibleTradeCount = f[42], RejectedTradeCount = f[43],
                LastPrice = ExactDecimal(f[44], f[45], f[46]), IsValid = f[47] != 0, IsReplayComplete = f[48] != 0,
                RecoveryGenerationId = Epoch(f[49], f[50]), InvalidReason = (FuturesVwapSourceInvalidReason)f[51]
            };
        return true;
    }
}

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Non-consuming coherent native state for a qualified instrument.
    /// Flags zero means unavailable during startup/reset/retirement. This read
    /// acquires no provider subscription and performs no I/O.</summary>
    public LivePublication ReadLiveState(MarketDataLane lane, uint instrument, ushort publisher)
    {
        lock (gate)
        {
            EnsureLiveAccess();
            NativeMarketDataLibrary.Check(library.StateLive(handle.Identity, (uint)lane, instrument, publisher, 656, out var state), "read native derived state");
            if (state.Size != 656 || state.Reserved != 0 || state.Flags > 63 || state.Source.Kind != 0 || state.Source.Size != 128)
                throw new InvalidDataException("Malformed native derived state.");
            return state;
        }
    }
    /// <summary>Drains up to sixteen coherent publications into reusable caller
    /// storage. Choose this or raw-event delivery: they consume the same FIFO.</summary>
    public unsafe int DrainLivePublications(Span<LivePublication> destination)
    {
        if (destination.Length > 16) throw new ArgumentOutOfRangeException(nameof(destination));
        lock (gate)
        {
            EnsureLiveAccess();
            uint count;
            fixed (LivePublication* records = destination)
                NativeMarketDataLibrary.Check(library.PublicationsLive(handle.Identity, records, (uint)destination.Length, out count), "drain live publications");
            if (count > destination.Length) throw new InvalidDataException("Native publication batch exceeded caller capacity.");
            for (var index = 0; index < count; index++)
            {
                ref var value = ref destination[index];
                if (value.Size != 656 || value.Reserved != 0 || value.Update > 6 || value.Flags > 63 || value.Source.Size != 128 || value.Source.Kind is < 1 or > 6
                    || (value.Source.Kind >= 5) != (value.Update >= 5) || value.Source.Kind >= 5 && value.Update != value.Source.Kind)
                    throw new InvalidDataException("Malformed native publication.");
            }
            return (int)count;
        }
    }
}
