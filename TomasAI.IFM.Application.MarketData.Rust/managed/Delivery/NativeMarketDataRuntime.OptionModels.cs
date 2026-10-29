using System.Buffers;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;
/// <summary>Copied canonical/provider reference mapping, not a source or lease.</summary>
public sealed record NativeOptionModelUnderlying(string Source,string Contract,ushort Publisher);
/// <summary>Exact native mapping result. No financial conversion occurs in its materializer.</summary>
[MessagePackObject]
public sealed record NativeOptionModelsResult([property:Key(0)]FuturesOptionContractReadModel[]? Models,[property:Key(1)]string? Failure,[property:Key(2)]string? Identity);
/// <summary>Exact configured futures transfer result.</summary>
[MessagePackObject]
public sealed record NativeConfiguredFutureResult([property:Key(0)]FuturesContractV3ReadModel[]? Models,[property:Key(1)]string? Failure,[property:Key(2)]string? Identity);
public sealed partial class NativeMarketDataRuntime
{
    internal unsafe FuturesContractV3ReadModel ReadConfiguredFuture(ulong token,string id,string source,string? fallback,bool onTheRun,bool rollover)
    {
        var call=library.CatalogOptionModels??throw new NotSupportedException("Native configured model mapping is unavailable.");
        var request=JsonSerializer.SerializeToUtf8Bytes(new{asset="futures",configured=true,canonical=id,symbol=source,fallback,sort=0,underlyings=Array.Empty<object>(),on_the_run=onTheRun,rollover});
        if(request.Length>16384)throw new ArgumentOutOfRangeException(nameof(id));
        var buffer=ArrayPool<byte>.Shared.Rent(65536);
        try{lock(gate){EnsureLiveAccess();uint length;
            fixed(byte* input=request)fixed(byte* output=buffer)NativeMarketDataLibrary.Check(call(handle.Identity,token,input,(uint)request.Length,output,65536,out length),"project native configured future");
            if(length is 0 or >65536)throw new InvalidDataException("Native configured future exceeds query bound.");
            var result=MessagePackSerializer.Deserialize<NativeConfiguredFutureResult>(buffer.AsMemory(0,(int)length),MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
            if(result.Failure=="MissingCurrency")throw new MarketDataContractMappingException(id,"both the contract currency and settlement currency are missing");
            if(result.Failure=="MissingMaturity")throw new MarketDataContractMappingException(id,"the futures maturity is missing");
            if(result.Failure=="InvalidMaturity")throw new DatabentoFeedException(DatabentoFeedStatus.AbiMismatch,"A native contract detail marked an invalid maturity date as present.");
            if(result.Failure is not null || result.Models?.Length!=1)throw new InvalidDataException("Malformed configured native futures result.");
            return result.Models[0];}}
        finally{ArrayPool<byte>.Shared.Return(buffer,clearArray:true);}
    }
    internal unsafe FuturesOptionContractReadModel[] ReadOptionModels(ulong token,string? fallback,NativeOptionModelUnderlying[] underlyings,bool orderByMaturity,DateOnly? from,DateOnly? through,string? configuredId,string? sourceSymbol)
    {
        ArgumentNullException.ThrowIfNull(underlyings);
        var call=library.CatalogOptionModels??throw new NotSupportedException("Native option DTO mapping is unavailable.");
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new{configured=configuredId is not null,canonical=configuredId,symbol=sourceSymbol,fallback,sort=orderByMaturity?2:1,from=from?.DayNumber,through=through?.DayNumber,
            underlyings=underlyings.Select(u=>new object[]{u.Source,u.Contract,u.Publisher})});
        if(bytes.Length>16384)throw new ArgumentOutOfRangeException(nameof(underlyings));
        const int maximum=4*1024*1024;var buffer=ArrayPool<byte>.Shared.Rent(maximum);
        try
        {
            lock(gate)
            {
                EnsureLiveAccess();uint length;
                fixed(byte* input=bytes)fixed(byte* output=buffer)NativeMarketDataLibrary.Check(call(handle.Identity,token,input,(uint)bytes.Length,output,maximum,out length),"project native option models");
                if(length is 0 or >maximum)throw new InvalidDataException("Native option models exceed query bounds.");
                var result=MessagePackSerializer.Deserialize<NativeOptionModelsResult>(buffer.AsMemory(0,(int)length),MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
                if(result is null || result.Models?.Length>4096)throw new InvalidDataException("Malformed native option model result.");
                return result.Failure switch{
                    null=>result.Models??throw new InvalidDataException("Missing native option models."),
                    "InvalidMaturity"=>throw new DatabentoFeedException(DatabentoFeedStatus.AbiMismatch,"A native contract detail marked an invalid maturity date as present."),
                    "MissingCurrency"=>throw new MarketDataContractMappingException(result.Identity!,"both the contract currency and settlement currency are missing"),
                    "MissingMaturity"=>throw new MarketDataContractMappingException(result.Identity!,"the option maturity is missing"),
                    "MissingStrike"=>throw new MarketDataContractMappingException(result.Identity!,"the option strike is missing"),
                    "LossyStrike"=>throw new MarketDataContractMappingException(result.Identity!,"the option strike cannot be represented by the existing double domain property without loss"),
                    _=>throw new InvalidDataException("Unknown native option model mapping failure.")};
            }
        }
        finally{ArrayPool<byte>.Shared.Return(buffer,clearArray:true);}
    }
}
