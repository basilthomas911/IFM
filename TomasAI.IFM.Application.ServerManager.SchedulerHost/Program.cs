using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;

var managedShutdown = args.Contains("--server-manager-stdin-shutdown");
args = args.Where(value => value != "--server-manager-stdin-shutdown").ToArray();
var settingsIndex = Array.IndexOf(args, "--settings");
var settings = settingsIndex >= 0 && settingsIndex + 1 < args.Length ? Path.GetFullPath(args[settingsIndex + 1]) : null;
var hostArgs = settingsIndex >= 0 ? args.Take(settingsIndex).Concat(args.Skip(settingsIndex + 2)).ToArray() : args;
using var host = SchedulerHostApplication.Create(hostArgs, configuration =>
{
    if (settings is not null) configuration.AddJsonFile(settings, optional: false, reloadOnChange: false).AddEnvironmentVariables();
});
if (managedShutdown)
{
    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    _ = Task.Run(async () =>
    {
        try
        {
            while (await Console.In.ReadLineAsync() is { } line)
                if (string.Equals(line, "shutdown", StringComparison.OrdinalIgnoreCase)) { lifetime.StopApplication(); return; }
            lifetime.StopApplication();
        }
        catch (IOException) { lifetime.StopApplication(); }
    });
}
await host.RunAsync();
