using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TomasAI.IFM.Application.ServerManager
{
    public class ConsoleVisibilityModel : ObservableObject
    {
        Visibility _visibility;
        
        public Visibility Value
        {
            get => _visibility;
            set => SetProperty(ref _visibility, value);
        }
    }
}
