using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using TomasAI.IFM.UI.Net.Views.App;
using WinForms=System.Windows.Forms;

using Microsoft.Extensions.Configuration;
using TomasAI.IFM.UI.Net;

namespace TomasAI.IFM.UI.Net
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] cmdLineArgs)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.

            WinForms.Application.ThreadException += Application_ThreadException;
            WinForms.Application.SetUnhandledExceptionMode(WinForms.UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            WinForms.Application.EnableVisualStyles();
            WinForms.Application.SetCompatibleTextRenderingDefault(false);
            WinForms.Application.SetHighDpiMode(HighDpiMode.SystemAware);
#pragma warning disable WFO5001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            //WinForms.Application.SetColorMode(SystemColorMode.Dark);
#pragma warning restore WFO5001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var config = AppSetup();
            var mainForm = Startup.Configure(config).GetForm<IFMAppView>();
            WinForms.Application.Run(new DelayedApplicationContext(mainForm, TimeSpan.FromSeconds(10)));
            Process.GetCurrentProcess().Kill();
        }

        sealed class DelayedApplicationContext : WinForms.ApplicationContext
        {
            readonly WinForms.Form _mainForm;
            readonly WinForms.Timer _timer;

            public DelayedApplicationContext(WinForms.Form mainForm, TimeSpan delay)
            {
                _mainForm = mainForm;
                _timer = new WinForms.Timer { Interval = checked((int)delay.TotalMilliseconds) };
                _timer.Tick += ShowMainForm;
                _timer.Start();
            }

            void ShowMainForm(object? sender, EventArgs e)
            {
                _timer.Stop();
                _timer.Dispose();
                MainForm = _mainForm;
                _mainForm.Show();
            }
        }

        static IConfigurationRoot AppSetup()
        {
            // Set up the configuration sources.
            var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    //.AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
            return builder.Build();
        }
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var errorMessage = new StringBuilder();
            errorMessage.AppendLine(((Exception)e.ExceptionObject).GetType().FullName);
            errorMessage.AppendLine(((Exception)e.ExceptionObject).Message);
            errorMessage.AppendLine(((Exception)e.ExceptionObject).StackTrace);
            MessageBox.Show($"{errorMessage}", "UnhandledException", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            Process.GetCurrentProcess().Kill();
        }

        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            var errorMessage = new StringBuilder();
            var ex = e.Exception;
            while (ex.InnerException != null)
                ex = ex.InnerException;
            errorMessage.AppendLine(ex.GetType().FullName);
            errorMessage.AppendLine(ex.Message);
            errorMessage.AppendLine(ex.StackTrace);
            MessageBox.Show($"{errorMessage}", "ThreadException", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            Process.GetCurrentProcess().Kill();
        }
    }
}
