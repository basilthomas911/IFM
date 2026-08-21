using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerPipeServer(
    SchedulerHostOptions options,
    SchedulerDashboardQueryService queryService,
    SchedulerOperationsService? operations,
    SchedulerOutputService? output,
    ILogger<SchedulerPipeServer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SchedulerPipeServer(
        SchedulerHostOptions options,
        SchedulerDashboardQueryService queryService,
        ILogger<SchedulerPipeServer> logger)
        : this(options, queryService, null, null, logger)
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = CreatePipe();
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

    private NamedPipeServerStream CreatePipe()
    {
        if (!options.UseOperatorGroupPipeAcl)
        {
            return new NamedPipeServerStream(
                options.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        }

        var security = new PipeSecurity();
        var serviceIdentity = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Scheduler Host service identity has no Windows SID.");
        security.AddAccessRule(new PipeAccessRule(serviceIdentity, PipeAccessRights.FullControl, AccessControlType.Allow));
        foreach (var group in options.AllowedOperatorGroups)
        {
            var sid = (SecurityIdentifier)new NTAccount(group).Translate(typeof(SecurityIdentifier));
            security.AddAccessRule(new PipeAccessRule(
                sid,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
        }

        return NamedPipeServerStreamAcl.Create(
            options.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }

    private async Task ProcessConnectionAsync(Stream stream, CancellationToken cancellationToken)
    {
        var request = await ReadFrameAsync<SchedulerPipeRequest>(stream, cancellationToken);
        SchedulerPipeResponse response;
        if (request.Version != SchedulerProtocol.Version)
        {
            response = Failure(request, "UnsupportedVersion", $"Protocol version {request.Version} is unsupported.");
        }
        else
        {
            try
            {
                response = await DispatchAsync(request, GetCallerIdentity(stream), cancellationToken);
            }
            catch (SchedulerValidationException exception)
            {
                response = Failure(request, "ValidationFailed", exception.Message);
            }
            catch (SchedulerConflictException exception)
            {
                response = Failure(request, "Conflict", exception.Message);
            }
            catch (JsonException exception)
            {
                response = Failure(request, "InvalidPayload", exception.Message);
            }
        }

        await WriteFrameAsync(stream, response, cancellationToken);
    }

    private async Task<SchedulerPipeResponse> DispatchAsync(
        SchedulerPipeRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var operationService = operations;
        SchedulerDashboardDto? dashboard = null;
        ScheduleValidationResultDto? validation = null;
        SchedulerOperationResultDto? result = null;
        TaskRunOutputPageDto? outputPage = null;
        switch (request.Operation)
        {
            case SchedulerProtocol.GetDashboardOperation:
                dashboard = await queryService.GetAsync(cancellationToken);
                break;
            case SchedulerProtocol.ValidateScheduleOperation:
                validation = RequireOperations(operationService).Validate(ReadPayload<ScheduleDefinitionInputDto>(request));
                break;
            case SchedulerProtocol.CreateScheduleOperation:
                result = await RequireOperations(operationService).CreateAsync(request.RequestId, actor, ReadPayload<ScheduleDefinitionInputDto>(request), cancellationToken);
                break;
            case SchedulerProtocol.UpdateScheduleOperation:
                result = await RequireOperations(operationService).UpdateAsync(
                    request.RequestId,
                    actor,
                    RequireExpectedVersion(request),
                    ReadPayload<ScheduleDefinitionInputDto>(request),
                    cancellationToken);
                break;
            case SchedulerProtocol.SetScheduleEnabledOperation:
                result = await RequireOperations(operationService).SetEnabledAsync(
                    request.RequestId,
                    actor,
                    RequireExpectedVersion(request),
                    ReadPayload<SetScheduleEnabledDto>(request),
                    request.Reason,
                    cancellationToken);
                break;
            case SchedulerProtocol.DeleteScheduleOperation:
                result = await RequireOperations(operationService).DeleteAsync(
                    request.RequestId,
                    actor,
                    RequireExpectedVersion(request),
                    ReadPayload<ScheduleIdentityDto>(request).ScheduleDefinitionId,
                    request.Reason,
                    cancellationToken);
                break;
            case SchedulerProtocol.RunNowOperation:
                result = await RequireOperations(operationService).RunNowAsync(
                    request.RequestId,
                    actor,
                    ReadPayload<RunNowRequestDto>(request).ScheduleDefinitionId,
                    request.Reason,
                    cancellationToken);
                break;
            case SchedulerProtocol.CancelRunOperation:
                result = await RequireOperations(operationService).CancelAsync(
                    request.RequestId,
                    actor,
                    ReadPayload<RunIdentityDto>(request).RunId,
                    request.Reason,
                    cancellationToken);
                break;
            case SchedulerProtocol.RetryRunOperation:
                result = await RequireOperations(operationService).RetryAsync(
                    request.RequestId,
                    actor,
                    ReadPayload<RunIdentityDto>(request).RunId,
                    request.Reason,
                    cancellationToken);
                break;
            case SchedulerProtocol.GetRunOutputOperation:
                outputPage = await (output ?? throw new SchedulerValidationException("Run output operations are unavailable."))
                    .GetPageAsync(ReadPayload<RunOutputRequestDto>(request), cancellationToken);
                break;
            case SchedulerProtocol.RunRetentionOperation:
                result = await RequireOperations(operationService).RunRetentionAsync(request.RequestId, actor, request.Reason, cancellationToken);
                break;
            default:
                return Failure(request, "UnsupportedOperation", $"Operation '{request.Operation}' is unsupported.");
        }

        return new SchedulerPipeResponse(
            SchedulerProtocol.Version,
            request.RequestId,
            true,
            null,
            null,
            dashboard,
            validation,
            result,
            outputPage);
    }

    private static SchedulerOperationsService RequireOperations(SchedulerOperationsService? service)
        => service ?? throw new SchedulerValidationException("Scheduler mutation operations are unavailable.");

    private static T ReadPayload<T>(SchedulerPipeRequest request) where T : class
        => request.Payload?.Deserialize<T>(JsonOptions)
            ?? throw new SchedulerValidationException($"Operation '{request.Operation}' requires a payload.");

    private static long RequireExpectedVersion(SchedulerPipeRequest request)
        => request.ExpectedVersion is > 0
            ? request.ExpectedVersion.Value
            : throw new SchedulerValidationException("A positive expected entity version is required.");

    private static string GetCallerIdentity(Stream stream)
    {
        if (stream is NamedPipeServerStream pipe)
        {
            try
            {
                var identity = pipe.GetImpersonationUserName();
                if (!string.IsNullOrWhiteSpace(identity))
                {
                    return identity;
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                // Current-user pipe authorization still applies; use the service identity for audit fallback.
            }
        }

        return $"{Environment.UserDomainName}\\{Environment.UserName}";
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
