using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using TomasAI.IFM.Views.App;
using WinForms=System.Windows.Forms;

namespace TomasAI.IFM
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] cmdLineArgs)
        {
            WinForms.Application.ThreadException += Application_ThreadException;
            WinForms.Application.SetUnhandledExceptionMode(WinForms.UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            WinForms.Application.EnableVisualStyles();
            WinForms.Application.SetCompatibleTextRenderingDefault(false);
            var mainForm = Startup.Configure().GetForm<IFMAppView>();
            WinForms.Application.Run(mainForm);
            Process.GetCurrentProcess().Kill();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var errorMessage = new StringBuilder();
            errorMessage.AppendLine(((Exception)e.ExceptionObject).GetType().FullName);
            errorMessage.AppendLine(((Exception)e.ExceptionObject).Message);
            errorMessage.AppendLine(((Exception)e.ExceptionObject).StackTrace);
            WinForms.MessageBox.Show($"{errorMessage}", "UnhandledException", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            Process.GetCurrentProcess().Kill();
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            var errorMessage = new StringBuilder();
            var ex = e.Exception;
            while (ex.InnerException != null)
                ex = ex.InnerException;
            errorMessage.AppendLine(ex.GetType().FullName);
            errorMessage.AppendLine(ex.Message);
            errorMessage.AppendLine(ex.StackTrace);
            WinForms.MessageBox.Show($"{errorMessage}", "ThreadException", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            Process.GetCurrentProcess().Kill();
        }
    }
}
