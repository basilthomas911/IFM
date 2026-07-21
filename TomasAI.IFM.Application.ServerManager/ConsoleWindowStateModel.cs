using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TomasAI.IFM.Application.ServerManager
{
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
