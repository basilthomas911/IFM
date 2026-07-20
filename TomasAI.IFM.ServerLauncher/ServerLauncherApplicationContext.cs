using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using WinForms=System.Windows.Forms;
using TomasAI.IFM.Shared.Log;

namespace TomasAI.IFM.Framework.ServerLauncher
{
    public class ServerLauncherApplicationContext : WinForms.ApplicationContext
    {
        private IFMServerLauncher _commandServer;
        private IFMServerLauncher _queryServer;
        private IFMServerLauncher _eventServer;

        public ServerLauncherApplicationContext()
        {
            var serverConsole = new IFMServerConsole();
            var notifyIcon = new WinForms.NotifyIcon();
            notifyIcon.Icon = Properties.Resources.AppIcon;
            notifyIcon.Text = "IFM Server Console";
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += (sender, e) => ViewConsole(serverConsole);
            var viewConsoleMenuItem = new WinForms.MenuItem("View Console", (sender, e) => ViewConsole(serverConsole));
            var resetServersMenuItem = new WinForms.MenuItem("Reset", (sender, e) => ResetServers(serverConsole));
            var exitMenuItem = new WinForms.MenuItem("Exit", (sender, e) => Exit(notifyIcon));
            notifyIcon.ContextMenu = new WinForms.ContextMenu(new WinForms.MenuItem[] { viewConsoleMenuItem, resetServersMenuItem, exitMenuItem });
            OpenServers(serverConsole);
        }
  
        void ViewConsole(IFMServerConsole serverConsole)
        {
            // If we are already showing the window, merely focus it.
            if (serverConsole.Visible)
                serverConsole.Activate();
            else
                serverConsole.ShowDialog();
        }

        void ResetServers(IFMServerConsole serverConsole)
        {
            serverConsole.Clear();
            CloseServers();
            OpenServers(serverConsole);
        }

        void Exit(WinForms.NotifyIcon notifyIcon)
        {
            // We must manually tidy up and remove the icon before we exit.
            // Otherwise it will be left behind until the user mouses over.
            CloseServers();
            notifyIcon.Visible = false;
            WinForms.Application.Exit();
        }

        void OpenServers(IFMServerConsole serverConsole)
        {
            _queryServer = new IFMServerLauncher(
                workingDirectory: ConfigurationManager.AppSettings["QueryWorkingDirectory"],
                exeName: ConfigurationManager.AppSettings["DotNetExe"],
                exeArguments: ConfigurationManager.AppSettings["QueryServerDll"],
                onDataReceived: e => serverConsole.AddServerLog(ServerLogType.Query, e.Data));
            _eventServer = new IFMServerLauncher(
                workingDirectory: ConfigurationManager.AppSettings["EventWorkingDirectory"],
                exeName: ConfigurationManager.AppSettings["DotNetExe"],
                exeArguments: ConfigurationManager.AppSettings["EventServerDll"],
                onDataReceived: e => serverConsole.AddServerLog(ServerLogType.Event, e.Data));
            _commandServer = new IFMServerLauncher(
                workingDirectory: ConfigurationManager.AppSettings["CommandWorkingDirectory"],
                exeName: ConfigurationManager.AppSettings["DotNetExe"],
                exeArguments: ConfigurationManager.AppSettings["CommandServerDll"],
                onDataReceived: e => serverConsole.AddServerLog(ServerLogType.Command, e.Data));
        }

        void CloseServers()
        {
            if (_commandServer != null)
            {
                _commandServer.Dispose();
                _commandServer = null;
            }
            if (_queryServer != null)
            {
                _queryServer.Dispose();
                _queryServer = null;
            }
            if (_eventServer != null)
            {
                _eventServer.Dispose();
                _eventServer = null;
            }
        }

    }
}
