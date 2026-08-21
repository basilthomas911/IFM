using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.Api.Server;

public static class ServerManagerStandardInputShutdown
{
    private const string EnableArgument = "--server-manager-stdin-shutdown";
    private const string ShutdownMessage = "shutdown";

    public static void EnableServerManagerStandardInputShutdown(
        this WebApplication application,
        IReadOnlyCollection<string> arguments,
        ILogger logger)
    {
        if (!arguments.Contains(EnableArgument, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _ = MonitorAsync(
            application.Services.GetRequiredService<IHostApplicationLifetime>(),
            logger);
    }

    private static async Task MonitorAsync(IHostApplicationLifetime lifetime, ILogger logger)
    {
        // Console.In can be backed by an anonymous process pipe whose asynchronous
        // read blocks synchronously until data arrives. Yield before beginning the
        // read so enabling the control channel can never delay host startup.
        await Task.Yield();

        try
        {
            while (!lifetime.ApplicationStopping.IsCancellationRequested)
            {
                var message = await Console.In.ReadLineAsync(lifetime.ApplicationStopping);
                if (message is null)
                {
                    return;
                }

                if (!string.Equals(message, ShutdownMessage, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Ignoring an unrecognized Server Manager standard-input control message.");
                    continue;
                }

                logger.LogInformation("Server Manager requested graceful API shutdown.");
                lifetime.StopApplication();
                return;
            }
        }
        catch (OperationCanceledException) when (lifetime.ApplicationStopping.IsCancellationRequested)
        {
            // Normal host shutdown ends the control reader.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Server Manager standard-input shutdown monitoring failed.");
        }
    }
}
