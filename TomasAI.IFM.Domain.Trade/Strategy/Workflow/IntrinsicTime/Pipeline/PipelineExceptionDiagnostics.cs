using System.Globalization;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Pipeline;

/// <summary>Creates bounded, readable exception diagnostics for persisted pipeline failures.</summary>
public static class PipelineExceptionDiagnostics
{
    const int MaximumValueLength = 4096;
    const int MaximumInnerExceptionDepth = 4;

    public static IReadOnlyDictionary<string, string> Create(
        Exception exception,
        IReadOnlyDictionary<string, string>? context = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var diagnostics = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context is not null)
            foreach (var pair in context)
                Add(diagnostics, pair.Key, pair.Value);

        var current = exception;
        for (var depth = 0; current is not null && depth < MaximumInnerExceptionDepth; depth++)
        {
            var prefix = depth == 0 ? "Exception" : $"InnerException.{depth}";
            Add(diagnostics, $"{prefix}.Type", current.GetType().FullName ?? current.GetType().Name);
            Add(diagnostics, $"{prefix}.Message", current.Message);
            Add(diagnostics, $"{prefix}.HResult", $"0x{current.HResult:X8}");
            Add(diagnostics, $"{prefix}.Source", current.Source);
            Add(diagnostics, $"{prefix}.Target", current.TargetSite is null
                ? null
                : $"{current.TargetSite.DeclaringType?.FullName}.{current.TargetSite.Name}");
            Add(diagnostics, $"{prefix}.StackTrace", current.StackTrace);
            current = current.InnerException;
        }

        if (current is not null)
            Add(diagnostics, "InnerException.Truncated", "Additional inner exceptions were omitted.");
        return diagnostics;
    }

    public static string Format(Exception exception, IReadOnlyDictionary<string, string>? context = null)
        => string.Join(';', Create(exception, context).Select(pair => $"{pair.Key}={pair.Value}"));

    public static string Summary(string operation, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(exception);
        return Limit($"{operation}: {Normalize(exception.Message)}", 1024);
    }

    static void Add(IDictionary<string, string> diagnostics, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        diagnostics[key] = Limit(Normalize(value), MaximumValueLength);
    }

    static string Normalize(string value) => value
        .Replace(Environment.NewLine, " | ", StringComparison.Ordinal)
        .Replace((char)13, ' ')
        .Replace((char)10, ' ')
        .Replace(';', ',');
    static string Limit(string value, int length)
        => value.Length <= length ? value : value[..(length - 14)] + "...[truncated]";
}