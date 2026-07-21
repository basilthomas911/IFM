using System;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TomasAI.IFM.Application.ServerManager
{
    public class ServerLauncher : IDisposable
    {
        static object _lock = new object();
        Process? _process;

        public ServerLauncher(string workingDirectory, string exeName, string exeArguments, Action<DataReceivedEventArgs> onDataReceived, int startUpDelay = 0)
        {
            Task.Run(() => {
                try
                {
                    lock (_lock)
                    {
                        Environment.CurrentDirectory = workingDirectory;
                        var startInfo = new ProcessStartInfo
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            RedirectStandardOutput = true,
                            FileName = $"{workingDirectory}\\{exeName}",
                            Arguments = !string.IsNullOrWhiteSpace(exeArguments) ? exeArguments : null,
                            WorkingDirectory = null
                        };
                        _process = new Process();
                        _process.StartInfo = startInfo;
                        _process.Start();
                        _process.OutputDataReceived += (sender, e) => onDataReceived(e);
                        _process.BeginOutputReadLine();
                    }
                    _process.WaitForExit();
                }
                catch
                {
                    _process?.Kill();
                    _process?.Close();
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
            if (_process is not null)
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

}
