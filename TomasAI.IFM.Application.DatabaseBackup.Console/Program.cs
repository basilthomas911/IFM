using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.DatabaseBackup.Console;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        global::System.Console.CancelKeyPress += cancelHandler;

        try
        {
            if (args.Length > 0 && args[0] is "help" or "--help" or "-h")
            {
                await global::System.Console.Out.WriteLineAsync(DatabaseBackupConsoleOptions.Usage)
                    .ConfigureAwait(false);
                return DatabaseBackupConsoleExitCodes.Success;
            }
            var options = DatabaseBackupConsoleOptions.Parse(args);
            await using var connectionManager = new NatsConnectionManager();
            var producer = new NatsActorProducer(
                new NatsProducerOptions { Url = options.NatsUrl },
                NullLogger.Instance,
                connectionManager);

            try
            {
                await producer.StartAsync(
                    new ActorMailboxId(ActorType.Query, "DatabaseBackupConsole"),
                    cancellation.Token).ConfigureAwait(false);
                var runner = new DatabaseBackupConsoleRunner(
                    new DatabaseBackupCommandApi(producer),
                    new DatabaseBackupQueryApi(producer),
                    global::System.Console.Out,
                    TimeProvider.System);
                return await runner.RunAsync(options, cancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                await producer.StopAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return DatabaseBackupConsoleExitCodes.Cancelled;
        }
        catch (ArgumentException exception)
        {
            await global::System.Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return DatabaseBackupConsoleExitCodes.InvalidArguments;
        }
        catch (Exception exception)
        {
            await global::System.Console.Error.WriteLineAsync(
                $"Database backup service unavailable: {exception.Message}").ConfigureAwait(false);
            return DatabaseBackupConsoleExitCodes.ServiceUnavailable;
        }
        finally
        {
            global::System.Console.CancelKeyPress -= cancelHandler;
        }
    }
}
