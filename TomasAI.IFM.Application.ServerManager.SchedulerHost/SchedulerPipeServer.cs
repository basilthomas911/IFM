using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerPipeServer(
    SchedulerHostOptions options,
    SchedulerDashboardQueryService queryService,
    ILogger<SchedulerPipeServer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    options.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stoppingToken);
                await ProcessConnectionAsync(pipe, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduler named-pipe connection failed.");
            }
        }
    }

    private async Task ProcessConnectionAsync(Stream stream, CancellationToken cancellationToken)
    {
        var request = await ReadFrameAsync<SchedulerPipeRequest>(stream, cancellationToken);
        SchedulerPipeResponse response;
        if (request.Version != SchedulerProtocol.Version)
        {
            response = Failure(request, "UnsupportedVersion", $"Protocol version {request.Version} is unsupported.");
        }
        else if (!string.Equals(request.Operation, SchedulerProtocol.GetDashboardOperation, StringComparison.Ordinal))
        {
            response = Failure(request, "UnsupportedOperation", $"Operation '{request.Operation}' is unsupported.");
        }
        else
        {
            var dashboard = await queryService.GetAsync(cancellationToken);
            response = new SchedulerPipeResponse(
                SchedulerProtocol.Version,
                request.RequestId,
                Success: true,
                ErrorCode: null,
                ErrorMessage: null,
                dashboard);
        }

        await WriteFrameAsync(stream, response, cancellationToken);
    }

    private static SchedulerPipeResponse Failure(SchedulerPipeRequest request, string code, string message)
        => new(SchedulerProtocol.Version, request.RequestId, false, code, message, null);

    internal static async Task<T> ReadFrameAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > SchedulerProtocol.MaximumFrameBytes)
        {
            throw new InvalidDataException($"Scheduler pipe frame length {length} is invalid.");
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new InvalidDataException("Scheduler pipe frame contained no object.");
    }

    internal static async Task WriteFrameAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if (payload.Length > SchedulerProtocol.MaximumFrameBytes)
        {
            throw new InvalidDataException("Scheduler pipe response exceeds the protocol frame limit.");
        }

        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
