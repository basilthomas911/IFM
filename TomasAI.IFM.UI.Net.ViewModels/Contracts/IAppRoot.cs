using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.UI.Net.Contracts;

public interface IAppRoot
{
    string AppEnvironment { get;  }
    TView GetForm<TView>() where TView : class;
    TModel GetModel<TModel>() where TModel : class;
    IStatusConsoleWriter GetStatusConsoleWriter();
    void Execute(Action modelAction);
}
