using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.UI.Net.Contracts;

public interface IAppRoot
{
    string AppEnvironment { get;  }
    TModel GetModel<TModel>() where TModel : class;
    IStatusConsoleWriter GetStatusConsoleWriter();
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
