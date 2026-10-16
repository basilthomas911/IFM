using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TomasAI.IFM.Application.ServerManager
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]
    public class ConsoleWindowStateModel : ObservableObject
    {
        WindowState _windowState;

        public WindowState Value
        {
            get => _windowState;
            set => SetProperty(ref _windowState, value);
        }
    }
}
