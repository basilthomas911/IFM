using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WinForms=System.Windows.Forms;

namespace TomasAI.IFM.Framework.ServerLauncher
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            WinForms.Application.EnableVisualStyles();
            WinForms.Application.SetCompatibleTextRenderingDefault(false);
            WinForms.Application.Run(new ServerLauncherApplicationContext());
        }
    }
}
