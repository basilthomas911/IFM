using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed class DatabentoLatestPriceClient : IDatabentoLatestPriceClient
{
    private readonly string _dataset;
    private readonly LatestPriceAdmissionControl _admissionControl;
    private readonly LatestPriceQueryInvoker _query;

    /// <summary>Initializes a new DatabentoLatestPriceClient instance.</summary>
    /// <param name="dataset">The Databento dataset identifier.</param>
    /// <param name="admissionControl">The admission control.</param>
    /// <param name="query">The query.</param>
    internal DatabentoLatestPriceClient(
        string dataset,
        LatestPriceAdmissionControl admissionControl,
        LatestPriceQueryInvoker? query = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        _dataset = dataset;
        _admissionControl = admissionControl
            ?? throw new ArgumentNullException(nameof(admissionControl));
        _query = query ?? QueryNative;
    }

    /// <summary>Retrieves the latest price according to the requested price-selection policy and timeout.</summary>
    /// <param name="request">The parameters identifying the requested provider data or session.</param>
    /// <param name="timeout">The maximum time allowed for the operation.</param>
    /// <returns>The latest price result.</returns>
    public LatestPriceResult64 GetLatestPrice(
        LatestPriceRequest request,
        TimeSpan timeout)
    {
        Validate(request, timeout);
        var deadline = new MonotonicDeadline(timeout);
        using var admission = _admissionControl.Acquire(
            _dataset,
            RequireRemaining(deadline, "Latest-price admission timed out."));
        var remainingMilliseconds = deadline.RemainingMilliseconds;
        if (remainingMilliseconds == 0)
        {
            throw new DatabentoFeedTimeoutException(
                "Latest-price admission consumed the request timeout.");
        }
        var result = _query(_dataset, request, remainingMilliseconds);
        ValidateResult(request.PricePolicy, result);
        return result;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation names the public request that owns the invalid property rather than an inaccessible nested field.")]
    private void Validate(LatestPriceRequest request, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Dataset, nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol, nameof(request));
        if (!string.Equals(request.Dataset, _dataset, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Latest-price request dataset '{request.Dataset}' does not match "
                + $"the client dataset '{_dataset}'.",
                nameof(request));
        }
        if (request.InputSymbology is not (
                DatabentoInputSymbology.RawSymbol
                or DatabentoInputSymbology.InstrumentId))
        {
            throw new ArgumentException(
                "Latest-price input symbology is invalid.", nameof(request));
        }
        if (request.PricePolicy is not (
                LatestPricePolicy.LastTrade
                or LatestPricePolicy.QuoteMidpoint
                or LatestPricePolicy.Bid
                or LatestPricePolicy.Ask))
        {
            throw new ArgumentException(
                "Latest-price selection policy is invalid.", nameof(request));
        }
        if (request.FreshnessPolicy == LatestPriceFreshnessPolicy.NextObserved)
        {
            if (request.ReplayLookback != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "NextObserved requires a zero replay lookback.", nameof(request));
            }
        }
        else if (request.FreshnessPolicy
                 == LatestPriceFreshnessPolicy.ReplayLookbackThenLive)
        {
            ToMilliseconds(
                request.ReplayLookback,
                nameof(request),
                "ReplayLookback must be positive and less than uint.MaxValue milliseconds.");
        }
        else
        {
            throw new ArgumentException(
                "Latest-price freshness policy is invalid.", nameof(request));
        }
        ToMilliseconds(
            timeout,
            nameof(timeout),
            "The latest-price timeout must be positive and less than uint.MaxValue milliseconds.");
    }

    private static void ValidateResult(
        LatestPricePolicy requestedPolicy,
        LatestPriceResult64 result)
    {
        if (result.InstrumentId == 0 || result.SelectedPolicy != requestedPolicy)
        {
            throw new DatabentoFeedException(
                DatabentoFeedStatus.AbiMismatch,
                "The native latest-price result did not identify the requested policy and instrument.");
        }
        var qualifying = requestedPolicy switch
        {
            LatestPricePolicy.LastTrade => result.HasLastTrade,
            LatestPricePolicy.QuoteMidpoint => result.HasBid && result.HasAsk
                && result.BidPrice <= result.AskPrice,
            LatestPricePolicy.Bid => result.HasBid,
            LatestPricePolicy.Ask => result.HasAsk,
            _ => false
        };
        if (!qualifying)
        {
            throw new DatabentoFeedException(
                DatabentoFeedStatus.AbiMismatch,
                $"The native latest-price result did not contain a qualifying {requestedPolicy} value.");
        }
    }

    private static TimeSpan RequireRemaining(
        MonotonicDeadline deadline,
        string message)
    {
        var remaining = deadline.Remaining;
        if (remaining <= TimeSpan.Zero)
        {
            throw new DatabentoFeedTimeoutException(message);
        }
        return remaining;
    }

    private static uint ToMilliseconds(
        TimeSpan value,
        string parameterName,
        string message)
    {
        if (value <= TimeSpan.Zero || value.TotalMilliseconds >= uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, message);
        }
        return checked((uint)Math.Ceiling(value.TotalMilliseconds));
    }

    private static unsafe LatestPriceResult64 QueryNative(
        string dataset,
        LatestPriceRequest request,
        uint timeoutMilliseconds)
    {
        var datasetBytes = Encoding.UTF8.GetBytes(dataset);
        var symbolBytes = Encoding.UTF8.GetBytes(request.Symbol);
        var utf8Blob = new byte[checked(datasetBytes.Length + symbolBytes.Length)];
        datasetBytes.CopyTo(utf8Blob, 0);
        symbolBytes.CopyTo(utf8Blob, datasetBytes.Length);
        fixed (byte* blobPointer = utf8Blob)
        {
            var nativeRequest = new NativeLatestPriceRequest
            {
                StructSize = checked((uint)Marshal.SizeOf<NativeLatestPriceRequest>()),
                AbiVersion = NativeConstants.AbiVersion,
                SelectedPolicy = request.PricePolicy,
                FreshnessPolicy = request.FreshnessPolicy,
                InputSymbology = request.InputSymbology,
                ReplayLookbackMilliseconds = request.FreshnessPolicy
                    == LatestPriceFreshnessPolicy.NextObserved
                        ? 0
                        : ToMilliseconds(
                            request.ReplayLookback,
                            nameof(request),
                            "ReplayLookback is outside the native range."),
                Dataset = new NativeUtf8Slice
                {
                    Offset = 0,
                    Length = checked((uint)datasetBytes.Length)
                },
                Symbol = new NativeUtf8Slice
                {
                    Offset = checked((uint)datasetBytes.Length),
                    Length = checked((uint)symbolBytes.Length)
                },
                Utf8Blob = blobPointer,
                Utf8BlobBytes = checked((uint)utf8Blob.Length)
            };
            var status = NativeMethods.GetLatestPrice(
                &nativeRequest,
                timeoutMilliseconds,
                out var result);
            NativeStatus.ThrowIfFailed(status, null, "Get latest price");
            return result;
        }
    }
}
