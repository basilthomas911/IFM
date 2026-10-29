using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Shared.MarketData.Contracts;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Copies the current FMP configuration and environment credential into
    /// this native owner. No provider request or child process is started. The
    /// temporary managed UTF-8 credential buffer is zeroed after the FFI call.</summary>
    public unsafe void ConfigureFmp(FinancialModelingPrepOptions options,string[] numericDnsServers)
    {
        ArgumentNullException.ThrowIfNull(options);ArgumentNullException.ThrowIfNull(numericDnsServers);
        if(!options.Enabled)throw new FinancialModelingPrepConfigurationException("FMP is disabled in this backend configuration.");
        var key=Environment.GetEnvironmentVariable(options.ApiKeyEnvironmentVariable);
        if(string.IsNullOrWhiteSpace(key))throw new FinancialModelingPrepConfigurationException("The configured FMP API-key environment variable is unavailable.");
        var data=JsonSerializer.SerializeToUtf8Bytes(new{
            baseAddress=options.BaseAddress?.AbsoluteUri,treasuryRatesEndpoint=options.TreasuryRatesEndpoint,economicCalendarEndpoint=options.EconomicCalendarEndpoint,
            latestTreasuryLookbackDays=options.LatestTreasuryLookbackDays,maximumProviderWindowDays=options.MaximumProviderWindowDays,maximumRequestRangeDays=options.MaximumRequestRangeDays,
            maximumResponseBytes=options.MaximumResponseBytes,maximumNormalizedRows=options.MaximumNormalizedRows,maximumConcurrentRequests=options.MaximumConcurrentRequests,
            maximumRetryAttempts=options.MaximumRetryAttempts,initialRetryDelayMs=options.InitialRetryDelay.TotalMilliseconds,maximumRetryDelayMs=options.MaximumRetryDelay.TotalMilliseconds,
            circuitBreakerFailureThreshold=options.CircuitBreakerFailureThreshold,circuitBreakerBreakDurationMs=options.CircuitBreakerBreakDuration.TotalMilliseconds,
            totalOperationTimeoutMs=options.TotalOperationTimeout.TotalMilliseconds,requestTimeoutMs=options.RequestTimeout.TotalMilliseconds,key,dns=numericDnsServers});
        try{
            if(data.Length>16384)throw new FinancialModelingPrepConfigurationException("FMP configuration exceeded its native byte capacity.");
            lock(gate){EnsureLiveAccess();fixed(byte* input=data){var code=library.FmpConfigure(handle.Identity,input,(uint)data.Length);if(code==1)throw new FinancialModelingPrepConfigurationException("Invalid or unbounded native FMP configuration.");NativeMarketDataLibrary.Check(code,"configure FMP");}}
        }finally{CryptographicOperations.ZeroMemory(data);}
    }
    /// <summary>Acquires exact source Treasury decimals through the native FMP owner.</summary>
    public async Task<TreasuryCurveSnapshot[]> ReadFmpTreasuryAsync(DateOnly from,DateOnly to,bool latest,TimeSpan budget,int maximumResponseBytes,CancellationToken token=default)
    {
        using var document=await ReadFmpAsync(latest?1u:0u,from,to,null,budget,maximumResponseBytes,token).ConfigureAwait(false);
        var rows=new List<TreasuryCurveSnapshot>();
        foreach(var row in document.RootElement.EnumerateArray()){
            var date=row.GetProperty("date").GetInt32();var points=new List<TreasuryRatePoint>();
            if(row.GetProperty("source").GetString()!="FinancialModelingPrep"||row.GetProperty("rates").GetArrayLength()!=12)throw new InvalidDataException("Invalid FMP Treasury source/tenors.");
            foreach(var point in row.GetProperty("rates").EnumerateArray()){
                var tenor=(TreasuryTenor)point.GetProperty("months").GetUInt16();if(!Enum.IsDefined(tenor))throw new InvalidDataException("Invalid FMP tenor.");
                var words=point.GetProperty("bits");if(words.GetArrayLength()!=4)throw new InvalidDataException("Invalid FMP decimal layout.");
                points.Add(new(tenor,new decimal(words.EnumerateArray().Select(x=>unchecked((int)x.GetUInt32())).ToArray())));
            }
            rows.Add(new(new(date/10000,date/100%100,date%100),points.ToImmutableArray(),DateTimeOffset.UnixEpoch.AddTicks(row.GetProperty("retrieved").GetInt64()),"FinancialModelingPrep"));
        }
        if(latest&&rows.Count>1)throw new InvalidDataException("FMP latest query returned multiple curves.");
        return rows.ToArray();
    }
    /// <summary>Acquires ordered source calendar events through native transport and normalization.</summary>
    public async Task<EconomicCalendarEntry[]> ReadFmpCalendarAsync(DateOnly from,DateOnly to,IReadOnlySet<string>? countries,TimeSpan budget,int maximumResponseBytes,CancellationToken token=default)
    {
        using var document=await ReadFmpAsync(2,from,to,countries,budget,maximumResponseBytes,token).ConfigureAwait(false);
        var rows=new List<EconomicCalendarEntry>();
        foreach(var row in document.RootElement.EnumerateArray()){
            var values=row.GetProperty("values");if(values.GetArrayLength()!=7||row.GetProperty("source").GetString()!="FinancialModelingPrep")throw new InvalidDataException("Invalid FMP calendar source/scalars.");
            string? Value(int i)=>values[i].ValueKind==JsonValueKind.Null?null:values[i].GetString();
            rows.Add(new(DateTimeOffset.UnixEpoch.AddTicks(row.GetProperty("at").GetInt64()),row.GetProperty("country").GetString()!,row.GetProperty("name").GetString()!,Value(0),Value(1),Value(2),Value(3),Value(4),Value(5),Value(6),DateTimeOffset.UnixEpoch.AddTicks(row.GetProperty("retrieved").GetInt64()),"FinancialModelingPrep"));
        }return rows.ToArray();
    }
    async Task<JsonDocument> ReadFmpAsync(uint mode,DateOnly from,DateOnly to,IReadOnlySet<string>? countries,TimeSpan budget,int maximumResponseBytes,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(budget<TimeSpan.Zero||budget>TimeSpan.FromMinutes(2))throw new ArgumentOutOfRangeException(nameof(budget));
        if(countries?.Count>256)throw new FinancialModelingPrepValidationException("FMP country filter capacity exceeded.");
        var query=JsonSerializer.SerializeToUtf8Bytes(new{from=ReferenceDate(from),to=ReferenceDate(to),countries=countries?.ToArray()??[]});if(query.Length>8192)throw new FinancialModelingPrepValidationException("FMP reference query capacity exceeded.");
        var started=Stopwatch.GetTimestamp();var request=SubmitRetained(library.FmpSubmit,mode,query,budget,token);
        try{
            try{await request.Completed.ConfigureAwait(false);}catch(TimeoutException error){throw new FinancialModelingPrepUnavailableException("The FMP operation exceeded its original deadline.",error);}
            token.ThrowIfCancellationRequested();if(Stopwatch.GetElapsedTime(started)>=budget)throw new FinancialModelingPrepUnavailableException("The FMP operation exceeded its original deadline.");
            return TakeFmp(request.Operation,maximumResponseBytes,token);
        }finally{lock(gate)if(referenceOperations.Remove(request.Operation))NativeMarketDataLibrary.Check(library.ReferenceDiscard(handle.Identity,request.Operation),"discard FMP reference result");}
    }
    JsonDocument TakeFmp(ulong operation,int maximumResponseBytes,CancellationToken token)
    {
        var document=TakeRetainedDocument(operation);
        try{
            var root=document.RootElement;
            if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("failure",out var failure))throw FmpFailure(failure,maximumResponseBytes,token);
            if(root.ValueKind!=JsonValueKind.Array||root.GetArrayLength()>10000)throw new InvalidDataException("Invalid native FMP result envelope.");
            return document;
        }catch{document.Dispose();throw;}
    }
    static Exception FmpFailure(JsonElement failure,int maximumResponseBytes,CancellationToken token)
    {
        var message=failure.GetProperty("message").GetString()??"FMP acquisition failed.";
        var status=failure.GetProperty("status");var http=status.ValueKind==JsonValueKind.Null?HttpStatusCode.ServiceUnavailable:(HttpStatusCode)status.GetInt32();
        return failure.GetProperty("code").GetString() switch{
            "Authentication"=>new FinancialModelingPrepAuthenticationException(http),"RateLimit"=>new FinancialModelingPrepRateLimitException(http),
            "Validation"=>new FinancialModelingPrepValidationException(message),"Configuration"=>new FinancialModelingPrepConfigurationException(message),
            "Contract"=>new FinancialModelingPrepContractException(message),"ResponseTooLarge"=>new FinancialModelingPrepResponseTooLargeException(maximumResponseBytes),
            "Response"=>new FinancialModelingPrepResponseException(message),"Cancelled"=>new OperationCanceledException(message,token),
            "Timeout" or "Unavailable"=>new FinancialModelingPrepUnavailableException(message),_=>new InvalidOperationException(message)};
    }
}
