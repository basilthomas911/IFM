using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Historical provider over the new DLL's cancellable HTTP/decoder owner.
/// Storage paths, approval budgets and durable job identity belong to application
/// orchestration. No existing Databento DLL or managed provider is invoked.</summary>
public sealed partial class NativeHistoricalProvider : IMarketDataHistoricalProvider, TomasAI.IFM.Application.MarketData.Contracts.Historical.IHistoricalProviderAvailability
{
    readonly NativeMarketDataRuntime runtime;
    readonly TimeSpan budget;
    readonly int maximumBatchRecords;
    /// <summary>Uses the configured native historical transport and one bounded
    /// query budget. An outer recovery cancellation token can only shorten it.</summary>
    public NativeHistoricalProvider(NativeMarketDataRuntime runtime,TimeSpan budget,int maximumBatchRecords=4096)
    {this.runtime=runtime??throw new ArgumentNullException(nameof(runtime));if(budget<=TimeSpan.Zero||budget>TimeSpan.FromMinutes(30))throw new ArgumentOutOfRangeException(nameof(budget));if(maximumBatchRecords is <1 or >65536)throw new ArgumentOutOfRangeException(nameof(maximumBatchRecords));this.budget=budget;this.maximumBatchRecords=maximumBatchRecords;}
    /// <summary>Reuses existing historical timeout/batch configuration. Synthetic
    /// provider selection is rejected explicitly; emulator implementation is separate.</summary>
    public NativeHistoricalProvider(NativeMarketDataRuntime runtime,TomasAI.IFM.Framework.MarketData.DataBento.DatabentoHistoricalProviderOptions options)
        :this(runtime,HistoricalBudget(options),options.MaximumBatchRecords){}
    static TimeSpan HistoricalBudget(TomasAI.IFM.Framework.MarketData.DataBento.DatabentoHistoricalProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if(options.UseSyntheticProvider)throw new ArgumentException("The native live backend does not select an emulator.",nameof(options));
        return TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);
    }
    /// <summary>Copies schema-specific availability from the cancellable native HTTP owner.</summary>
    public async ValueTask<DateTimeOffset> GetAvailableEndAsync(HistoricalProviderRequest request, CancellationToken cancellationToken)
    {
        using var result = await runtime.ReadHistoricalControlAsync(4, request, null, budget, cancellationToken).ConfigureAwait(false);
        return DateTimeOffset.UnixEpoch.AddTicks(result.RootElement.GetProperty("end").GetInt64() / 100);
    }
    /// <summary>Returns the provider's exact cost/byte/record estimate under one deadline.</summary>
    public async ValueTask<HistoricalProviderEstimate> EstimateAsync(HistoricalProviderRequest request,CancellationToken cancellationToken)
    {
        using var result=await runtime.ReadHistoricalControlAsync(0,request,null,budget,cancellationToken).ConfigureAwait(false);var row=result.RootElement;
        return new((decimal)row.GetProperty("cost").GetDouble(),checked((long)row.GetProperty("bytes").GetUInt64()),checked((long)row.GetProperty("records").GetUInt64()),DateTimeOffset.UnixEpoch.AddTicks(row.GetProperty("observed").GetInt64()));
    }
    /// <summary>Submits one approved paid batch exactly once, without automatic retry.</summary>
    public async ValueTask<HistoricalProviderJob> SubmitBatchAsync(HistoricalProviderRequest request,CancellationToken cancellationToken)
    {using var result=await runtime.ReadHistoricalControlAsync(1,request,null,budget,cancellationToken).ConfigureAwait(false);return Job(result.RootElement);}
    /// <summary>Reads an existing durable provider job without creating new billable work.</summary>
    public async ValueTask<HistoricalProviderJob> GetBatchJobAsync(string providerJobId,CancellationToken cancellationToken)
    {using var result=await runtime.ReadHistoricalControlAsync(2,null,providerJobId,budget,cancellationToken).ConfigureAwait(false);return Job(result.RootElement);}
    /// <summary>Returns immutable DBN file descriptors, preserving baseline file filtering.</summary>
    public async ValueTask<IReadOnlyList<HistoricalProviderFile>> ListBatchFilesAsync(string providerJobId,CancellationToken cancellationToken)
    {
        using var result=await runtime.ReadHistoricalControlAsync(3,null,providerJobId,budget,cancellationToken).ConfigureAwait(false);
        return result.RootElement.GetProperty("files").EnumerateArray().Select(row=>new HistoricalProviderFile(row.GetProperty("providerFileId").GetString()!,row.GetProperty("fileName").GetString()!,(HistoricalDataSchema)row.GetProperty("schema").GetUInt32(),checked((long)row.GetProperty("sizeBytes").GetUInt64()),row.GetProperty("sha256").GetString()!)).ToArray();
    }
    static HistoricalProviderJob Job(JsonElement row)
    {
        // Batch cost follows the baseline Rust Display literal before CLR JSON
        // decimal decoding, rather than estimate's distinct binary64 cast path.
        using var cost=JsonDocument.Parse(row.GetProperty("costUsd").GetString()!);
        return new(row.GetProperty("providerJobId").GetString()!,Enum.Parse<HistoricalProviderJobState>(row.GetProperty("state").GetString()!,true),cost.RootElement.GetDecimal(),checked((long)row.GetProperty("recordCount").GetUInt64()),checked((long)row.GetProperty("billedBytes").GetUInt64()),row.GetProperty("progressPercent").GetByte(),string.Empty);
    }
}
