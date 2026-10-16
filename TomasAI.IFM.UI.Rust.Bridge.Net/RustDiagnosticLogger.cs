using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Writes structured client diagnostics even when the native UI has no console.</summary>
internal sealed class RustDiagnosticLogger : ILogger
{
    static readonly object Gate = new();
    readonly string path;
    public RustDiagnosticLogger(string? path = null) => this.path = path ?? Environment.GetEnvironmentVariable("IFM_UI_DIAGNOSTIC_LOG") ?? Path.Combine(AppContext.BaseDirectory, "logs", $"rust-ui-{Environment.ProcessId}.mpklog");
    readonly Microsoft.Extensions.Logging.LoggerExternalScopeProvider scopes = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => scopes.Push(state);
    public bool IsEnabled(LogLevel level) => level >= LogLevel.Information && level != LogLevel.None;
    public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level)) return;
        var context = new List<object?>();
        scopes.ForEachScope((scope, target) => target.Add(scope), context);
        var record = BridgeDocument.Encode(new { utc = DateTime.UtcNow, processId = Environment.ProcessId, level = level.ToString(), eventId = eventId.Id, message = formatter(state, exception), fields = state is IEnumerable<KeyValuePair<string, object?>> values ? values.ToDictionary(x => x.Key, x => x.Value?.ToString()) : new Dictionary<string,string?> { ["state"] = state?.ToString() }, scopes = context, exception = exception?.ToString() },camelCase:true);
        try { lock (Gate) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); using var stream=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read);Span<byte> length=stackalloc byte[4];BinaryPrimitives.WriteInt32LittleEndian(length,record.Length);stream.Write(length);stream.Write(record); } }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Console.Error.WriteLine($"Client diagnostic log unavailable: {error.Message}"); }
    }
}
