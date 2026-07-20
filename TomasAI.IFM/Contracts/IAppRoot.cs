using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Shared.Log.ServiceApi;

namespace TomasAI.IFM.Contracts
{
    public interface IAppRoot
    {
        string AppEnvironment { get;  }
        TWindowsForm GetForm<TWindowsForm>() where TWindowsForm: Form;
        TModel GetModel<TModel>() where TModel : class;
        IStatusConsoleWriter GetStatusConsoleWriter();
        void Execute(Action modelAction);
    }
}
