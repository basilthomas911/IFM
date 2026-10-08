using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;

var settingsIndex = Array.IndexOf(args, "--settings");
var settings = settingsIndex >= 0 && settingsIndex + 1 < args.Length ? Path.GetFullPath(args[settingsIndex + 1]) : null;
var hostArgs = settingsIndex >= 0 ? args.Take(settingsIndex).Concat(args.Skip(settingsIndex + 2)).ToArray() : args;
await SchedulerHostApplication.Create(hostArgs, configuration =>
{
    if (settings is not null) configuration.AddJsonFile(settings, optional: false, reloadOnChange: false).AddEnvironmentVariables();
}).RunAsync();
