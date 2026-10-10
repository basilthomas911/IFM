using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.UI.Net.Views.Presentation;

/// <summary>Provides the process-wide, non-throwing exception boundary for the IFM UI.</summary>
public static class UiExceptionReporter
{
    static readonly Action<ILogger, string, string, string, int, bool, Exception?> LogUnhandled =
        LoggerMessage.Define<string, string, string, int, bool>(
            LogLevel.Error,
            new EventId(61001, nameof(LogUnhandled)),
            "Unhandled UI exception. Source={Source}; Operation={Operation}; Component={Component}; ManagedThreadId={ManagedThreadId}; Fatal={Fatal}");

    static readonly Action<ILogger, string, string, string, long, double, Exception?> LogUiDelay =
        LoggerMessage.Define<string, string, string, long, double>(
            LogLevel.Warning, new EventId(61002, "StrategyDetailsUiDelay"),
            "Slow strategy details UI operation. Method={Method}; NodePath={NodePath}; WorkflowId={WorkflowId}; WorkflowRevision={WorkflowRevision}; ElapsedMs={ElapsedMs}");

    /// <summary>Records completed slow UI operations without logging normal clicks or serializing workflow objects.</summary>
    public static void ReportUiDelay(string method, string nodePath, string workflowId, long revision, double elapsedMs)
    {
        if (elapsedMs < 100) return;
        try
        {
            var configuredLogger = Volatile.Read(ref logger);
            if (configuredLogger?.IsEnabled(LogLevel.Warning) == true)
                LogUiDelay(configuredLogger, method, nodePath, workflowId, revision, elapsedMs, null);
        }
        catch { /* Diagnostic logging must not break UI navigation. */ }
    }

    /// <summary>Records live selection latency without logging every quote.</summary>
    public static void ReportOptionChainReady(string ownerId, DateOnly maturity, string contracts, double elapsedMs)
    {
        Volatile.Read(ref logger)?.LogInformation("Option chain four-leg readiness; OwnerId={OwnerId}; Maturity={Maturity}; Contracts={Contracts}; ElapsedMilliseconds={ElapsedMilliseconds}",
            ownerId, maturity, contracts, elapsedMs);
    }

    static ILogger? logger;

    /// <summary>Configures the structured logger used by all UI exception boundaries.</summary>
    public static void Configure(ILogger configuredLogger) =>
        Volatile.Write(ref logger, configuredLogger ?? throw new ArgumentNullException(nameof(configuredLogger)));

    /// <summary>Records an exception without ever throwing back into the UI.</summary>
    public static void Report(
        Exception exception,
        string source,
        string operation,
        object? component = null,
        bool fatal = false)
    {
        try
        {
            var componentName = component switch
            {
                Control control when !string.IsNullOrWhiteSpace(control.Name) =>
                    $"{control.GetType().Name}:{control.Name}",
                null => "Application",
                _ => component.GetType().Name
            };
            Debug.WriteLine(
                $"Unhandled UI exception. Source={source}; Operation={operation}; Component={componentName}; Fatal={fatal}{Environment.NewLine}{exception}");
            var configuredLogger = Volatile.Read(ref logger);
            if (configuredLogger is not null)
                LogUnhandled(configuredLogger, source, operation, componentName,
                    Environment.CurrentManagedThreadId, fatal, exception);
            else
            {
                var fallbackEntry =
                    $"{DateTimeOffset.UtcNow:O} Unhandled UI exception. Source={source}; Operation={operation}; Component={componentName}; ManagedThreadId={Environment.CurrentManagedThreadId}; Fatal={fatal}{Environment.NewLine}{exception}{Environment.NewLine}";
                Console.Error.WriteLine(
                    $"Unhandled UI exception. Source={source}; Operation={operation}; Component={componentName}; ManagedThreadId={Environment.CurrentManagedThreadId}; Fatal={fatal}{Environment.NewLine}{exception}");
                var fallbackDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(fallbackDirectory);
                File.AppendAllText(Path.Combine(fallbackDirectory, "ifm-ui-bootstrap.log"), fallbackEntry);
            }
        }
        catch
        {
            // Exception reporting is the final boundary and must never throw.
        }
    }

    /// <summary>Observes a fire-and-forget task and records every unexpected failure.</summary>
    public static void Observe(
        Task task,
        string operation,
        object? component = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.IsCompleted)
        {
            ObserveCompletion(task, operation, component, cancellationToken);
            return;
        }
        var continuation = task.ContinueWith(
            static (completed, state) =>
            {
                var observation = ((string Operation, object? Component,
                    CancellationToken CancellationToken))state!;
                ObserveCompletion(completed, observation.Operation, observation.Component,
                    observation.CancellationToken);
            },
            (operation, component, cancellationToken),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        GC.KeepAlive(continuation);
    }

    /// <summary>Observes a fire-and-forget value task and records every unexpected failure.</summary>
    public static void Observe(
        ValueTask task,
        string operation,
        object? component = null,
        CancellationToken cancellationToken = default) =>
        Observe(task.AsTask(), operation, component, cancellationToken);

    /// <summary>Runs an asynchronous UI operation inside a logging exception boundary.</summary>
    public static async Task RunAsync(
        Func<Task> operation,
        string operationName,
        object? component = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Report(exception, "UiAsyncOperation", operationName, component);
        }
    }

    static void ObserveCompletion(
        Task task,
        string operation,
        object? component,
        CancellationToken cancellationToken)
    {
        try
        {
            if (task.IsCompletedSuccessfully)
                return;
            if (task.IsCanceled && cancellationToken.IsCancellationRequested)
                return;
            if (task.Exception is { } aggregate)
                Report(aggregate.Flatten().InnerExceptions.Count == 1
                        ? aggregate.Flatten().InnerExceptions[0]
                        : aggregate.Flatten(),
                    "FireAndForgetTask", operation, component);
            else if (task.IsCanceled)
                Report(new TaskCanceledException(task), "FireAndForgetTask", operation, component);
        }
        catch (Exception exception)
        {
            Report(exception, "TaskObservation", operation, component);
        }
    }
}
