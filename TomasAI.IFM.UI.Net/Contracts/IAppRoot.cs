using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.UI.Net.Contracts;

public interface IAppRoot
{
    string AppEnvironment { get;  }
    TWindowsForm GetForm<TWindowsForm>() where TWindowsForm: Form;
    TModel GetModel<TModel>() where TModel : class;
    IStatusConsoleWriter GetStatusConsoleWriter();
    void Execute(Action modelAction);
}
