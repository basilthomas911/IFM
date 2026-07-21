using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TomasAI.IFM.UI.Net.Contracts
{
    public interface IControlCommand
    {
        void Load(IAppRoot appRoot, Action<bool> dataLoaded);
        void Unload();
        void Add(Action<bool> addAction);
        void Change(Action<bool> changeAction);
        void Remove();
        void Import();
        bool Close(Action<bool> changeAction);
        bool CanChangeRemove { get; }
        bool CanImport { get; }
    }
}
