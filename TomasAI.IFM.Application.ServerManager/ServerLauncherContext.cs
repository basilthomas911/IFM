using System;
using System.Threading.Tasks;
using System.Windows;
using WinForms=System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.ServerManager;

public class ServerLauncherContext : WinForms.ApplicationContext
{
    ServerLauncher? _telemetryServer;
    ServerLauncher? _commandServer;
    ServerLauncher? _queryServer;
    ServerLauncher? _eventServer;
    ServerLauncher? _predictiveModelServer;
    WinForms.NotifyIcon _notifyIcon;

    public ServerLauncherContext(App serverApp)
    {
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = Resource1.AppIcon,
            Text = "IFM Server Manager",
            Visible = true
        };
        _notifyIcon.ContextMenuStrip = new WinForms.ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.Add("View Console", null, (sender, e) => ViewConsole(serverApp)).Name = "ViewConsole";
        _notifyIcon.ContextMenuStrip.Items.Add("Minimize Console", null, (sender, e) => MinimizeConsole(serverApp)).Name = "MinimizeConsole";
        _notifyIcon.ContextMenuStrip.Items.Add("Reset", null, (sender, e) => ResetServers(serverApp)).Name = "Reset";
        _notifyIcon.DoubleClick += (sender, e) => ViewConsole(serverApp);
        serverApp.Exit += (sender, e) => Exit(serverApp, _notifyIcon);

        var viewModel = serverApp.ServiceProvider.GetRequiredService<IMainWindowViewModel>();
        viewModel.ConsoleVisibility = Visibility.Hidden;
        viewModel.ConsoleWindowState = WindowState.Minimized;
        
        var console = serverApp.ServiceProvider.GetRequiredService<MainWindow>();
        console.Show();

        StartServers(serverApp);
    }

    void ViewConsole(App serverApp)
    {
        _notifyIcon.ContextMenuStrip.Items["ViewConsole"].Enabled = false;
        var viewModel = serverApp.ServiceProvider.GetRequiredService<IMainWindowViewModel>();
        viewModel.ConsoleVisibility = Visibility.Visible;
        viewModel.ConsoleWindowState = WindowState.Maximized;
       }

    void MinimizeConsole(App serverApp)
    {
        // If we are already showing the window, merely focus it.
        _notifyIcon.ContextMenuStrip.Items["ViewConsole"].Enabled = true;
        var viewModel = serverApp.ServiceProvider.GetRequiredService<IMainWindowViewModel>();
        viewModel.ConsoleVisibility = Visibility.Hidden;
        viewModel.ConsoleWindowState = WindowState.Minimized;

    }

    void ResetServers(App serverApp)
    {
        //serverConsole.Clear();
        StopServers();
        StartServers(serverApp);
    }

    void Exit(App serverApp, WinForms.NotifyIcon notifyIcon)
    {
        // We must manually tidy up and remove the icon before we exit.
        // Otherwise it will be left behind until the user mouses over.
        StopServers();
        notifyIcon.Visible = false;
        serverApp.Shutdown();
    }

    void StartServers(App serverApp)
    {
        var viewModel = serverApp.ServiceProvider.GetRequiredService<IMainWindowViewModel>();
        _telemetryServer = new ServerLauncher(
            workingDirectory: serverApp.Configuration.GetValue<string>("ServerManager:Telemetry:WorkingDirectory") ?? "",
            exeName: serverApp.Configuration.GetValue<string>("ServerManager:Telemetry:ExeName") ?? "",
            exeArguments: "",
            onDataReceived: e => viewModel.AddServerLog(ServerLogType.Telemetry, e.Data));
        Task.Delay(TimeSpan.FromSeconds(5)).Wait();
        _commandServer = new ServerLauncher(
            workingDirectory: serverApp.Configuration.GetValue<string>("ServerManager:Command:WorkingDirectory") ?? "",
            exeName: serverApp.Configuration.GetValue<string>("ServerManager:Command:ExeName") ?? "",
            exeArguments: "",
            onDataReceived: e => viewModel.AddServerLog(ServerLogType.Command, e.Data));
       // Task.Delay(TimeSpan.FromSeconds(2)).Wait();
        _eventServer = new ServerLauncher(
            workingDirectory: serverApp.Configuration.GetValue<string>("ServerManager:Event:WorkingDirectory") ?? "",
            exeName: serverApp.Configuration.GetValue<string>("ServerManager:Event:ExeName") ?? "",
            exeArguments: "",
            onDataReceived: e => viewModel.AddServerLog(ServerLogType.Event, e.Data));
        //Task.Delay(TimeSpan.FromSeconds(2)).Wait();
        _queryServer = new ServerLauncher(
            workingDirectory: serverApp.Configuration.GetValue<string>("ServerManager:Query:WorkingDirectory") ?? "",
            exeName: serverApp.Configuration.GetValue<string>("ServerManager:Query:ExeName") ?? "",
            exeArguments: "",
            onDataReceived: e => viewModel.AddServerLog(ServerLogType.Query, e.Data));
        _predictiveModelServer = new ServerLauncher(
             workingDirectory: serverApp.Configuration.GetValue<string>("ServerManager:PredictiveModel:WorkingDirectory") ?? "",
             exeName: serverApp.Configuration.GetValue<string>("ServerManager:PredictiveModel:ExeName") ?? "",
             exeArguments: "",
             onDataReceived: e => viewModel.AddServerLog(ServerLogType.Query, e.Data));
    }

    void StopServers()
    {
        if (_predictiveModelServer is not null)
        {
            _predictiveModelServer.Dispose();
            _predictiveModelServer = null;
        }
        if (_commandServer is not null)
        {
            _commandServer.Dispose();
            _commandServer = null;
        }
        if (_queryServer is not null)
        {
            _queryServer.Dispose();
            _queryServer = null;
        }
        if (_eventServer is not null)
        {
            _eventServer.Dispose();
            _eventServer = null;
        }
        if (_telemetryServer is not null)
        {
            _telemetryServer.Dispose();
            _telemetryServer = null;
        }
    }

}
