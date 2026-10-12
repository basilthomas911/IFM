namespace TomasAI.IFM.Domain.MarketData;

/// <summary>Compiled structured diagnostics for storage migration and provider boundaries.</summary>
internal static partial class StorageMigrationDiagnosticLog
{
    [global::Microsoft.Extensions.Logging.LoggerMessage(SkipEnabledCheck = true, EventId = 8904, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "{Component}.{Method} completed; UnderlyingContractId={UnderlyingContractId}; ExpiryDate={ExpiryDate}; FrozenPreviewOnly={FrozenPreviewOnly}; ReleaseOnly={ReleaseOnly}; Outcome={Outcome}; ErrorCode={ErrorCode}; ElapsedMilliseconds={ElapsedMilliseconds}")]
    internal static partial void Write4(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, string UnderlyingContractId, global::System.DateOnly ExpiryDate, bool FrozenPreviewOnly, bool ReleaseOnly, string Outcome, int ErrorCode, double ElapsedMilliseconds);

    [global::Microsoft.Extensions.Logging.LoggerMessage(SkipEnabledCheck = true, EventId = 8905, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Strategy option universe unavailable: ParameterSetId={ParameterSetId}, Version={Version}, ValueDate={ValueDate}")]
    internal static partial void Write5(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, global::System.Guid ParameterSetId, int Version, global::System.DateOnly ValueDate);

    [global::Microsoft.Extensions.Logging.LoggerMessage(SkipEnabledCheck = true, EventId = 8906, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "{Component}.{Method} frozen option chain UnderlyingContractId={UnderlyingContractId} {Expiry}: {Count} contracts, projected quotes {QuoteCount}; definitions {DefinitionsMs:F1} ms, inputs/window {InputsMs:F1} ms, bulk quotes/pricing {EvaluationMs:F1} ms, total {TotalMs:F1} ms.")]
    internal static partial void Write6(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, string UnderlyingContractId, global::System.DateOnly Expiry, int Count, int QuoteCount, double DefinitionsMs, double InputsMs, double EvaluationMs, double TotalMs);

    [global::Microsoft.Extensions.Logging.LoggerMessage(SkipEnabledCheck = true, EventId = 8907, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "Cached option chain {Root} {Expiry}: {Selected} of {Available} contracts in {Method}; current futures price {Price}, daily Bollinger deviation {Deviation} from {ObservationDate}.")]
    internal static partial void Write7(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Root, global::System.DateOnly Expiry, int Selected, int Available, string Method, decimal? Price, decimal? Deviation, global::System.DateOnly? ObservationDate);
}
