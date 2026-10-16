using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public sealed class SchedulerPipeClient(SchedulerClientOptions options) : ISchedulerDashboardClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SchedulerDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return Offline("Scheduler dashboard access is disabled in Server Manager configuration.");
        }

        SchedulerPipeResponse response;
        try
        {
            response = await (SendAsync(new SchedulerPipeRequest(
                SchedulerProtocol.Version,
                Guid.NewGuid(),
                SchedulerProtocol.GetDashboardOperation,
                DateTimeOffset.UtcNow), cancellationToken)).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Offline(
                $"Scheduler Host is offline or did not accept a connection within {options.ConnectTimeoutMilliseconds} ms.");
        }
        return response.Dashboard
            ?? throw new InvalidOperationException("Scheduler dashboard response contained no dashboard.");
    }

    public async Task<ScheduleValidationResultDto> ValidateScheduleAsync(
        ScheduleDefinitionInputDto input,
        CancellationToken cancellationToken)
        => (await (SendAsync(CreateRequest(SchedulerProtocol.ValidateScheduleOperation, input), cancellationToken)).ConfigureAwait(true)).Validation
            ?? throw new InvalidOperationException("Scheduler validation response contained no result.");

    public async Task<SchedulerOperationResultDto> CreateScheduleAsync(
        ScheduleDefinitionInputDto input,
        CancellationToken cancellationToken)
        => RequireOperation(await (SendAsync(CreateRequest(SchedulerProtocol.CreateScheduleOperation, input), cancellationToken)).ConfigureAwait(true));

    public async Task<SchedulerOperationResultDto> UpdateScheduleAsync(
        ScheduleDefinitionInputDto input,
        long expectedVersion,
        CancellationToken cancellationToken)
        => RequireOperation(await (SendAsync(CreateRequest(SchedulerProtocol.UpdateScheduleOperation, input, expectedVersion), cancellationToken)).ConfigureAwait(true));

    public async Task<SchedulerOperationResultDto> SetScheduleEnabledAsync(
        Guid scheduleId,
        bool enabled,
        long expectedVersion,
        string reason,
        CancellationToken cancellationToken)
        => RequireOperation(await (SendAsync(
            CreateRequest(
                SchedulerProtocol.SetScheduleEnabledOperation,
                new SetScheduleEnabledDto(scheduleId, enabled),
                expectedVersion,
                reason),
            cancellationToken)).ConfigureAwait(true));

    public async Task<SchedulerOperationResultDto> DeleteScheduleAsync(
        Guid scheduleId,
        long expectedVersion,
        string reason,
        CancellationToken cancellationToken)
        => RequireOperation(await (SendAsync(
            CreateRequest(SchedulerProtocol.DeleteScheduleOperation, new ScheduleIdentityDto(scheduleId), expectedVersion, reason),
            cancellationToken)).ConfigureAwait(true));

    public async Task<SchedulerOperationResultDto> RunNowAsync(Guid scheduleId, string reason, CancellationToken cancellationToken)
        => RequireOperation(await (SendAsync(
            CreateRequest(SchedulerProtocol.RunNowOperation, new RunNowRequestDto(scheduleId), reason: reason),
            cancellationToken)).ConfigureAwait(true));

    public async Task<SchedulerOperationResultDto> CancelRunAsync(Guid runId, string reason, CancellationToken cancellationToken)
        => RequireOperation(await (SendAsync(
            CreateRequest(SchedulerProtocol.CancelRunOperation, new RunIdentityDto(runId), reason: reason),
            cancellationToken)).ConfigureAwait(true));

    public async Task<SchedulerOperationResultDto> RetryRunAsync(Guid runId, string reason, CancellationToken cancellationToken)
        => RequireOperation(await (SendAsync(
            CreateRequest(SchedulerProtocol.RetryRunOperation, new RunIdentityDto(runId), reason: reason),
            cancellationToken)).ConfigureAwait(true));

    public async Task<TaskRunOutputPageDto> GetRunOutputAsync(RunOutputRequestDto request, CancellationToken cancellationToken)
        => (await (SendAsync(CreateRequest(SchedulerProtocol.GetRunOutputOperation, request), cancellationToken)).ConfigureAwait(true)).OutputPage
            ?? throw new InvalidOperationException("Scheduler output response contained no page.");

    private async Task<SchedulerPipeResponse> SendAsync(SchedulerPipeRequest request, CancellationToken cancellationToken)
    {
var pipe = new NamedPipeClientStream(
            ".",
            options.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
await using var pipeDisposal = pipe.ConfigureAwait(true);
        using var timeout = new CancellationTokenSource(options.ConnectTimeoutMilliseconds);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        await (pipe.ConnectAsync(linked.Token)).ConfigureAwait(true);

        await (WriteFrameAsync(pipe, request, linked.Token)).ConfigureAwait(true);
        var response = await (ReadFrameAsync<SchedulerPipeResponse>(pipe, linked.Token)).ConfigureAwait(true);
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
        await (stream.ReadExactlyAsync(header, cancellationToken)).ConfigureAwait(true);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > SchedulerProtocol.MaximumFrameBytes)
        {
            throw new InvalidDataException($"Scheduler pipe frame length {length} is invalid.");
        }

        var payload = new byte[length];
        await (stream.ReadExactlyAsync(payload, cancellationToken)).ConfigureAwait(true);
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
        await (stream.WriteAsync(header, cancellationToken)).ConfigureAwait(true);
        await (stream.WriteAsync(payload, cancellationToken)).ConfigureAwait(true);
        await (stream.FlushAsync(cancellationToken)).ConfigureAwait(true);
    }
}
