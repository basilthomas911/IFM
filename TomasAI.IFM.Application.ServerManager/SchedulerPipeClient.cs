using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager;

public interface ISchedulerDashboardClient
{
    Task<SchedulerDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
}

public sealed class SchedulerPipeClient(SchedulerClientOptions options) : ISchedulerDashboardClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SchedulerDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return Offline("Scheduler dashboard access is disabled in Server Manager configuration.");
        }

        await using var pipe = new NamedPipeClientStream(
            ".",
            options.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(options.ConnectTimeoutMilliseconds);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        await pipe.ConnectAsync(linked.Token);

        var request = new SchedulerPipeRequest(
            SchedulerProtocol.Version,
            Guid.NewGuid(),
            SchedulerProtocol.GetDashboardOperation,
            DateTimeOffset.UtcNow);
        await WriteFrameAsync(pipe, request, linked.Token);
        var response = await ReadFrameAsync<SchedulerPipeResponse>(pipe, linked.Token);
        if (response.RequestId != request.RequestId)
        {
            throw new InvalidDataException("Scheduler response request ID does not match the request.");
        }

        if (!response.Success || response.Dashboard is null)
        {
            throw new InvalidOperationException(
                $"Scheduler query failed ({response.ErrorCode ?? "Unknown"}): {response.ErrorMessage}");
        }

        return response.Dashboard;
    }

    private static SchedulerDashboardDto Offline(string message)
        => new(
            new SchedulerHealthDto(
                SchedulerServiceState.Unhealthy,
                "unknown",
                false,
                false,
                false,
                message,
                DateTimeOffset.UtcNow),
            [],
            [],
            [],
            DateTimeOffset.UtcNow);

    private static async Task<T> ReadFrameAsync<T>(Stream stream, CancellationToken cancellationToken)
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
            ?? throw new InvalidDataException("Scheduler pipe response contained no object.");
    }

    private static async Task WriteFrameAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if (payload.Length > SchedulerProtocol.MaximumFrameBytes)
        {
            throw new InvalidDataException("Scheduler pipe request exceeds the protocol frame limit.");
        }

        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
