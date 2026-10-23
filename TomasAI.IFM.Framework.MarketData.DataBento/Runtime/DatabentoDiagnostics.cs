using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record DatabentoProviderDiagnostic(int Kind, int Code, bool SessionFatal, bool IsLast, string Message)
{
    public string CodeName => Kind is not (1 or 2 or 3) ? "UNKNOWN_DIAGNOSTIC_KIND" : Kind == 3 ? ((DatabentoFeedStatus)Code).ToString() : Kind == 1 ? Code switch
    {
        1 => "AUTH_FAILED", 2 => "API_KEY_DEACTIVATED", 3 => "CONNECTION_LIMIT_EXCEEDED",
        4 => "SYMBOL_RESOLUTION_FAILED", 5 => "INVALID_SUBSCRIPTION", 6 => "INTERNAL_ERROR",
        7 => "SKIPPED_RECORDS_AFTER_SLOW_READING", 8 => "REPLAY_DATA_AGED_OUT", 255 => "UNSET", _ => "UNKNOWN"
    } : Code switch { 0 => "HEARTBEAT", 1 => "SUBSCRIPTION_ACK", 2 => "SLOW_READER_WARNING", 3 => "REPLAY_COMPLETED", 4 => "END_OF_INTERVAL", _ => "UNKNOWN_SYSTEM_CODE" };
}

/// <summary>Receives provider diagnostics without truncating codes or text. Sink exceptions never escape into native code.</summary>
public static unsafe partial class DatabentoDiagnostics
{
    static Action<DatabentoProviderDiagnostic>? sink;
    static long errors;
    static string? lastFailure;
    public static long ErrorCount => Interlocked.Read(ref errors);
    public static string? LastFailure => Volatile.Read(ref lastFailure);
    public static void Install(Action<DatabentoProviderDiagnostic> receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        Volatile.Write(ref sink, receiver);
        SetCallback(&Receive);
    }

    [LibraryImport(DatabentoNativeLibraryResolver.LibraryName, EntryPoint = "dbf_set_diagnostic_callback")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void SetCallback(delegate* unmanaged[Cdecl]<int, int, int, int, byte*, uint, void> callback);

    internal static string Redact(string message)
    {
        foreach (var name in new[] { "DATABENTO_API_KEY", "IFM_DATASET_WORKER_BOOTSTRAP" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } secret) message = message.Replace(secret, "[redacted]", StringComparison.Ordinal);
        return message;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static void Receive(int kind, int code, int fatal, int isLast, byte* text, uint length)
    {
        try
        {
            var message = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(text, checked((int)length)));
            message = Redact(message);
            var diagnostic = new DatabentoProviderDiagnostic(kind, code, fatal != 0, isLast != 0, message);
            if (kind != 2) { Volatile.Write(ref lastFailure, diagnostic.CodeName + ": " + message); Interlocked.Increment(ref errors); }
            Volatile.Read(ref sink)?.Invoke(diagnostic);
        }
        catch (Exception error)
        {
            Volatile.Write(ref lastFailure, "Provider diagnostic sink failed: " + error.GetType().Name);
            Interlocked.Increment(ref errors);
        }
    }
}
