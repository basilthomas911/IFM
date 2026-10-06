using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.UI.Net.Contracts;

public interface IAppRoot
{
    /// <summary>Gets the operational logger; test roots may keep diagnostics disabled.</summary>
    Microsoft.Extensions.Logging.ILogger DiagnosticLogger => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    /// <summary>Gets the configured application environment name.</summary>
    string AppEnvironment { get; }

    /// <summary>Gets the typed UI domain-service catalog.</summary>
    IUiServiceCatalog Services { get; }

    /// <summary>Gets the authoritative, non-null futures value-date provider.</summary>
    IValueDateProvider ValueDates => FuturesValueDateProvider.System;

    /// <summary>Gets the application status-console writer.</summary>
    IStatusConsoleWriter GetStatusConsoleWriter();

    /// <summary>Executes an application operation with observable cancellation and failure.</summary>
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
