using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.UI.Net.Services;

namespace TomasAI.IFM.UI.Net.Contracts;

public interface IAppRoot
{
    /// <summary>Gets the configured application environment name.</summary>
    string AppEnvironment { get;  }

    /// <summary>Gets the typed UI domain-service catalog.</summary>
    IUiServiceCatalog Services { get; }

    /// <summary>Gets the application status-console writer.</summary>
    IStatusConsoleWriter GetStatusConsoleWriter();

    /// <summary>Executes an application operation with observable cancellation and failure.</summary>
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
