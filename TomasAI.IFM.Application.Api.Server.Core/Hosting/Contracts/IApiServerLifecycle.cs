namespace TomasAI.IFM.Application.Api.Server.Core.Hosting.Contracts;

/// <summary>Runs selected host mode using the executable-owned service provider.</summary>
public interface IApiServerLifecycle
{
    Task RunAsync(WebApplication app, string[] args, ILogger logger);
}
