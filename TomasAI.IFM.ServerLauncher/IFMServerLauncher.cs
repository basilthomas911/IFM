using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TomasAI.IFM.Framework.ServerLauncher
{
    public class IFMServerLauncher : IDisposable
    {
        Process _process;

        public IFMServerLauncher(string workingDirectory, string exeName, string exeArguments, Action<DataReceivedEventArgs> onDataReceived, int startUpDelay = 0)
        {
            Task.Run(() => {
                try
                {
                    var startInfo = new ProcessStartInfo();
                    startInfo.CreateNoWindow = true;
                    startInfo.UseShellExecute = false;
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.FileName = exeName;
                    startInfo.Arguments = !string.IsNullOrWhiteSpace(exeArguments) ? exeArguments : null;
                    startInfo.WorkingDirectory = workingDirectory;
                    _process = new Process();
                    _process.StartInfo = startInfo;
                    _process.Start();
                    _process.OutputDataReceived += (sender, e) => onDataReceived(e);
                    _process.BeginOutputReadLine();
                    _process.WaitForExit();
                }
                catch
                {
                    _process.Kill();
                    _process.Close();
                }
                finally
                {
                    _process = null;
                }
            });
            if (startUpDelay != 0)
                Task.Delay(TimeSpan.FromSeconds(startUpDelay)).Wait();
        }

        public void Dispose()
        {
            if (_process != null)
            {
                try
                {
                    _process.Kill();
                    _process.Close();
                    _process = null;
                }
                catch { }
                finally
                {
                    _process = null;
                }
            }
        }
    }

    public class IFMServerShutdown 
    {
        private Process _process;

        public IFMServerShutdown(string workingDirectory, string exeName, string exeArguments)
        {
            Task.Run(() => {
                var startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.RedirectStandardOutput = true;
                startInfo.FileName = exeName;
                startInfo.Arguments = !string.IsNullOrWhiteSpace(exeArguments) ? exeArguments : null;
                startInfo.WorkingDirectory = workingDirectory;
                _process = new Process();
                _process.StartInfo = startInfo;
                _process.Start();
                _process.WaitForExit();
                _process.Kill();
                _process.Close();
            });
        }

    }

}
