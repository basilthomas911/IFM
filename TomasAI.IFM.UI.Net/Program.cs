using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TomasAI.IFM.UI.Net.Views.App;
using WinForms = System.Windows.Forms;

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

            try
            {
                var config = AppSetup();
                var navigator = Startup.Configure(config);
                var mainForm = navigator.CreateView<IFMAppView>();
                WinForms.Application.Run(new NatsReadyApplicationContext(mainForm));
            }
            catch (Exception exception)
            {
                Environment.ExitCode = 1;
                ShowFatalError(exception, "Application Startup Error");
            }
        }

        static IConfigurationRoot AppSetup()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            var configuration = builder.Build();
            ApplyEnvironmentOverride("IFM_UI_NATS_URL", "AppSettings:NatsServerUri");
            ApplyEnvironmentOverride(
                "IFM_UI_NATS_STARTUP_TIMEOUT_SECONDS",
                "AppSettings:NatsStartupTimeoutSeconds");
            return configuration;

            void ApplyEnvironmentOverride(string variable, string key)
            {
                var value = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrWhiteSpace(value))
                    configuration[key] = value;
            }
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var errorMessage = new StringBuilder();
            errorMessage.AppendLine(((Exception)e.ExceptionObject).GetType().FullName);
            errorMessage.AppendLine(((Exception)e.ExceptionObject).Message);
            errorMessage.AppendLine(((Exception)e.ExceptionObject).StackTrace);
            Console.Error.WriteLine(errorMessage);
            WinForms.MessageBox.Show($"{errorMessage}", "UnhandledException", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            Environment.ExitCode = 1;
            WinForms.Application.Exit();
        }

        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            var errorMessage = new StringBuilder();
            var exception = e.Exception;
            while (exception.InnerException != null)
                exception = exception.InnerException;
            errorMessage.AppendLine(exception.GetType().FullName);
            errorMessage.AppendLine(exception.Message);
            errorMessage.AppendLine(exception.StackTrace);
            Console.Error.WriteLine(errorMessage);
            WinForms.MessageBox.Show($"{errorMessage}", "ThreadException", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            Environment.ExitCode = 1;
            WinForms.Application.Exit();
        }

        static void ShowFatalError(Exception exception, string caption)
        {
            var errorMessage = new StringBuilder();
            for (var current = exception; current is not null; current = current.InnerException)
            {
                errorMessage.AppendLine(current.GetType().FullName);
                errorMessage.AppendLine(current.Message);
                errorMessage.AppendLine(current.StackTrace);
            }

            Console.Error.WriteLine($"{caption}:{Environment.NewLine}{errorMessage}");

            WinForms.MessageBox.Show(
                $"{errorMessage}",
                caption,
                WinForms.MessageBoxButtons.OK,
                WinForms.MessageBoxIcon.Error);
        }

        /// <summary>
        /// Keeps the WinForms message loop on the STA thread while NATS starts and stops asynchronously.
        /// The main view is shown only after the shared NATS producer has connected successfully.
        /// </summary>
        sealed class NatsReadyApplicationContext : WinForms.ApplicationContext
        {
            readonly WinForms.Form _mainForm;
            readonly WinForms.Timer _lifecycleTimer;
            Task _lifecycleOperation;
            LifecyclePhase _phase = LifecyclePhase.Starting;

            public NatsReadyApplicationContext(WinForms.Form mainForm)
            {
                _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
                _mainForm.FormClosed += MainForm_FormClosed;
                _lifecycleOperation = Startup.StartAsync().AsTask();
                _lifecycleTimer = new WinForms.Timer { Interval = 25 };
                _lifecycleTimer.Tick += LifecycleTimer_Tick;
                _lifecycleTimer.Start();
            }

            void LifecycleTimer_Tick(object? sender, EventArgs e)
            {
                if (!_lifecycleOperation.IsCompleted)
                    return;

                if (_phase == LifecyclePhase.Starting)
                {
                    if (_lifecycleOperation.IsCompletedSuccessfully)
                    {
                        _phase = LifecyclePhase.Running;
                        _lifecycleTimer.Stop();
                        if (ShouldStartMaximized())
                            _mainForm.WindowState = WinForms.FormWindowState.Maximized;
                        _mainForm.Show();
                        return;
                    }

                    Environment.ExitCode = 1;
                    ShowFatalError(
                        _lifecycleOperation.Exception?.GetBaseException()
                            ?? new InvalidOperationException("NATS startup was cancelled."),
                        "Application Startup Error");
                    BeginShutdown();
                    return;
                }

                if (_phase != LifecyclePhase.Stopping)
                    return;

                if (!_lifecycleOperation.IsCompletedSuccessfully)
                {
                    Environment.ExitCode = 1;
                    ShowFatalError(
                        _lifecycleOperation.Exception?.GetBaseException()
                            ?? new InvalidOperationException("Application shutdown was cancelled."),
                        "Application Shutdown Error");
                }

                _lifecycleTimer.Stop();
                _lifecycleTimer.Tick -= LifecycleTimer_Tick;
                _lifecycleTimer.Dispose();
                _mainForm.FormClosed -= MainForm_FormClosed;
                ExitThread();
            }

            void MainForm_FormClosed(object? sender, WinForms.FormClosedEventArgs e) => BeginShutdown();

            static bool ShouldStartMaximized()
            {
                var value = Environment.GetEnvironmentVariable("IFM_UI_START_MAXIMIZED");
                return string.Equals(value, "1", StringComparison.Ordinal)
                    || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            }

            void BeginShutdown()
            {
                if (_phase == LifecyclePhase.Stopping)
                    return;

                _phase = LifecyclePhase.Stopping;
                _lifecycleOperation = Startup.ShutdownAsync().AsTask();
                _lifecycleTimer.Start();
            }

            enum LifecyclePhase
            {
                Starting,
                Running,
                Stopping
            }
        }
    }
}
