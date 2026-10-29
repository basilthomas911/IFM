using MessagePack;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>Copied canonical/provider identity. It has no source or lease handle.</summary>
[MessagePackObject]
public sealed record NativeOwnerContractBinding([property:Key(0)] MarketDataLane Lane,[property:Key(1)] DateOnly ValueDate,
    [property:Key(2)] uint Instrument,[property:Key(3)] ushort Publisher,[property:Key(4)] bool Active,[property:Key(5)] bool Healthy,
    [property:Key(6)] Guid Worker,[property:Key(7)] Guid Generation);
/// <summary>Native mark selection distinguishes unknown identity, unavailable value and typed business failure.</summary>
[MessagePackObject]
public sealed record NativeOwnerPrice([property:Key(0)] bool Mapped,[property:Key(1)] decimal? Price,[property:Key(2)] string? Failure);

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Commands a native transient route. Only copied owner identity
    /// crosses the ABI; C# retains no route registry, lease or renewal timer.</summary>
    public unsafe bool ChangeDatasetOwnerRoute(TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner owner,string contract,bool option,bool acquire)
    {
        owner.Validate();ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new{workflow_type=owner.WorkflowType,workflow_id=owner.WorkflowId,leg_id=owner.LegId,contract,options=option,acquire});
        lock(gate){EnsureLiveAccess();var call=library.OwnerRoute??throw new NotSupportedException("Native stream ownership is unavailable.");
            fixed(byte* input=bytes){NativeMarketDataLibrary.Check(call(handle.Identity,input,(uint)bytes.Length,out var changed),"change native stream owner");
                return changed switch{0=>false,1=>true,_=>throw new InvalidDataException("Invalid native stream-owner result.")};}}
    }
    /// <summary>Asks Rust whether an individual runtime tick route is current.</summary>
    public bool IsDatasetOwnerStreamActive(string contract)=>OwnerQuery<bool>(new{kind="active",contract,options=false});
    /// <summary>Copies a native-joined monitored ticker. The adapter only
    /// materializes public DTOs; source/risk coherence is decided in Rust.</summary>
    public TomasAI.IFM.Shared.MarketData.Contracts.Ticker.OptionTickerPriceSnapshot? ReadDatasetOwnerOptionTicker(string contract)
        =>OwnerQuery<Publication.NativeOptionTicker?>(new{kind="ticker",contract,options=true})?.ToSnapshot();
    /// <summary>Copies native publication-price state, including suppressed-trade
    /// continuity. This creates no managed last-price or aggregation cache.</summary>
    public TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceSnapshot? ReadDatasetOwnerMarketPrice(string contract)
        =>OwnerQuery<TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceSnapshot?>(new{kind="market",contract,options=false});
    /// <summary>Copies native-selected authoritative session statistics.</summary>
    public TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.FuturesSessionStatisticsSnapshot? ReadDatasetOwnerStatistics(string contract)
        =>OwnerQuery<TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.FuturesSessionStatisticsSnapshot?>(new{kind="statistics",contract,options=false});
    /// <summary>Queries exact canonical identity without a managed registration registry.</summary>
    public NativeOwnerContractBinding? ReadDatasetOwnerBinding(string contract,bool option)
        =>OwnerQuery<NativeOwnerContractBinding?>(new{kind="binding",contract,options=option});
    /// <summary>Rust selects raw trade/quote, qualifies freshness and calculates the mark.
    /// The adapter supplies immutable policy duration and performs no financial arithmetic.</summary>
    public NativeOwnerPrice ReadDatasetOwnerPrice(string contract,bool option,TimeSpan maximumAge)
        =>OwnerQuery<NativeOwnerPrice>(new{kind="price",contract,options=option,maximum_age_ticks=maximumAge.Ticks});
    /// <summary>Reads one native-joined canonical quote/risk result. No C#
    /// registration lookup, source-time join or Greek coherence policy runs here.</summary>
    public Publication.NativeCanonicalQuoteRisk? ReadDatasetOwnerQuoteRisk(string contract,DateOnly valueDate)
        =>OwnerQuery<Publication.NativeCanonicalQuoteRisk?>(new{kind="quote-risk",contract,options=true,date=valueDate.DayNumber});
    /// <summary>Reads one native-joined, durably acknowledged canonical trade/risk result.</summary>
    public Publication.NativeTradeGreeksResult? ReadDatasetOwnerTradeRisk(string contract,DateOnly valueDate)
        =>OwnerQuery<Publication.NativeTradeGreeksResult?>(new{kind="trade-risk",contract,options=true,date=valueDate.DayNumber});
    unsafe T OwnerQuery<T>(object arguments)
    {
        var command=JsonSerializer.SerializeToUtf8Bytes(arguments);
        if(command.Length>4096)throw new ArgumentOutOfRangeException(nameof(arguments));
        var output=System.Buffers.ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            uint length;lock(gate){EnsureLiveAccess();var query=library.OwnerQuery??throw new NotSupportedException("Native canonical query is unavailable.");
                fixed(byte* input=command)fixed(byte* buffer=output)NativeMarketDataLibrary.Check(query(handle.Identity,input,(uint)command.Length,buffer,4096,out length),"query native canonical market data");}
            if(length is 0 or >4096)throw new InvalidDataException("Native canonical query exceeded its bound.");
            return MessagePackSerializer.Deserialize<T>(output.AsMemory(0,(int)length),MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
        }
        finally{System.Buffers.ArrayPool<byte>.Shared.Return(output,clearArray:true);}
    }
}
