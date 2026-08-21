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

    Task<ScheduleValidationResultDto> ValidateScheduleAsync(ScheduleDefinitionInputDto input, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    Task<SchedulerOperationResultDto> CreateScheduleAsync(ScheduleDefinitionInputDto input, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    Task<SchedulerOperationResultDto> UpdateScheduleAsync(ScheduleDefinitionInputDto input, long expectedVersion, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    Task<SchedulerOperationResultDto> SetScheduleEnabledAsync(Guid scheduleId, bool enabled, long expectedVersion, string reason, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    Task<SchedulerOperationResultDto> DeleteScheduleAsync(Guid scheduleId, long expectedVersion, string reason, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    Task<SchedulerOperationResultDto> RunNowAsync(Guid scheduleId, string reason, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    Task<SchedulerOperationResultDto> CancelRunAsync(Guid runId, string reason, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    Task<SchedulerOperationResultDto> RetryRunAsync(Guid runId, string reason, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    Task<TaskRunOutputPageDto> GetRunOutputAsync(RunOutputRequestDto request, CancellationToken cancellationToken)
        => throw new NotSupportedException();
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

        var response = await SendAsync(new SchedulerPipeRequest(
            SchedulerProtocol.Version,
            Guid.NewGuid(),
            SchedulerProtocol.GetDashboardOperation,
            DateTimeOffset.UtcNow), cancellationToken);
        return response.Dashboard
            ?? throw new InvalidOperationException("Scheduler dashboard response contained no dashboard.");
    }

    public async Task<ScheduleValidationResultDto> ValidateScheduleAsync(
        ScheduleDefinitionInputDto input,
        CancellationToken cancellationToken)
        => (await SendAsync(CreateRequest(SchedulerProtocol.ValidateScheduleOperation, input), cancellationToken)).Validation
            ?? throw new InvalidOperationException("Scheduler validation response contained no result.");

    public async Task<SchedulerOperationResultDto> CreateScheduleAsync(
        ScheduleDefinitionInputDto input,
        CancellationToken cancellationToken)
        => RequireOperation(await SendAsync(CreateRequest(SchedulerProtocol.CreateScheduleOperation, input), cancellationToken));

    public async Task<SchedulerOperationResultDto> UpdateScheduleAsync(
        ScheduleDefinitionInputDto input,
        long expectedVersion,
        CancellationToken cancellationToken)
        => RequireOperation(await SendAsync(CreateRequest(SchedulerProtocol.UpdateScheduleOperation, input, expectedVersion), cancellationToken));

    public async Task<SchedulerOperationResultDto> SetScheduleEnabledAsync(
        Guid scheduleId,
        bool enabled,
        long expectedVersion,
        string reason,
        CancellationToken cancellationToken)
        => RequireOperation(await SendAsync(
            CreateRequest(
                SchedulerProtocol.SetScheduleEnabledOperation,
                new SetScheduleEnabledDto(scheduleId, enabled),
                expectedVersion,
                reason),
            cancellationToken));

    public async Task<SchedulerOperationResultDto> DeleteScheduleAsync(
        Guid scheduleId,
        long expectedVersion,
        string reason,
        CancellationToken cancellationToken)
        => RequireOperation(await SendAsync(
            CreateRequest(SchedulerProtocol.DeleteScheduleOperation, new ScheduleIdentityDto(scheduleId), expectedVersion, reason),
            cancellationToken));

    public async Task<SchedulerOperationResultDto> RunNowAsync(Guid scheduleId, string reason, CancellationToken cancellationToken)
        => RequireOperation(await SendAsync(
            CreateRequest(SchedulerProtocol.RunNowOperation, new RunNowRequestDto(scheduleId), reason: reason),
            cancellationToken));

    public async Task<SchedulerOperationResultDto> CancelRunAsync(Guid runId, string reason, CancellationToken cancellationToken)
        => RequireOperation(await SendAsync(
            CreateRequest(SchedulerProtocol.CancelRunOperation, new RunIdentityDto(runId), reason: reason),
            cancellationToken));

    public async Task<SchedulerOperationResultDto> RetryRunAsync(Guid runId, string reason, CancellationToken cancellationToken)
        => RequireOperation(await SendAsync(
            CreateRequest(SchedulerProtocol.RetryRunOperation, new RunIdentityDto(runId), reason: reason),
            cancellationToken));

    public async Task<TaskRunOutputPageDto> GetRunOutputAsync(RunOutputRequestDto request, CancellationToken cancellationToken)
        => (await SendAsync(CreateRequest(SchedulerProtocol.GetRunOutputOperation, request), cancellationToken)).OutputPage
            ?? throw new InvalidOperationException("Scheduler output response contained no page.");

    private async Task<SchedulerPipeResponse> SendAsync(SchedulerPipeRequest request, CancellationToken cancellationToken)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            options.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(options.ConnectTimeoutMilliseconds);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        await pipe.ConnectAsync(linked.Token);

        await WriteFrameAsync(pipe, request, linked.Token);
        var response = await ReadFrameAsync<SchedulerPipeResponse>(pipe, linked.Token);
        if (response.RequestId != request.RequestId)
        {
            throw new InvalidDataException("Scheduler response request ID does not match the request.");
        }

        if (!response.Success)
        {
            throw new InvalidOperationException(
                $"Scheduler query failed ({response.ErrorCode ?? "Unknown"}): {response.ErrorMessage}");
        }

        return response;
    }

    private static SchedulerPipeRequest CreateRequest<T>(
        string operation,
        T payload,
        long? expectedVersion = null,
        string? reason = null)
        => new(
            SchedulerProtocol.Version,
            Guid.NewGuid(),
            operation,
            DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(payload, JsonOptions),
            expectedVersion,
            reason);

    private static SchedulerOperationResultDto RequireOperation(SchedulerPipeResponse response)
        => response.OperationResult
            ?? throw new InvalidOperationException("Scheduler operation response contained no result.");

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
