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
            Console.In,
            application.Services.GetRequiredService<IHostApplicationLifetime>(),
            logger);
    }

    public static async Task MonitorAsync(
        TextReader input,
        IHostApplicationLifetime lifetime,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(logger);

        // Console.In can be backed by an anonymous process pipe whose asynchronous
        // read blocks synchronously until data arrives. Yield before beginning the
        // read so enabling the control channel can never delay host startup.
        await Task.Yield();

        try
        {
            while (!lifetime.ApplicationStopping.IsCancellationRequested)
            {
                var message = await input.ReadLineAsync(lifetime.ApplicationStopping);
                if (message is null)
                {
                    logger.LogInformation(
                        "Server Manager standard-input channel closed; requesting graceful API shutdown.");
                    lifetime.StopApplication();
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
