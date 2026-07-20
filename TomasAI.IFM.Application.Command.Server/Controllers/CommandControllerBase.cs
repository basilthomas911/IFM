using System.Net;
using Microsoft.AspNetCore.Mvc;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Command.Server.Controllers;

/// <summary>
/// Serves as a base class for controllers that handle command execution and posting operations.
/// </summary>
/// <remarks>This class provides protected methods for executing commands synchronously or asynchronously and
/// posting commands to a command service queue. It also includes internal methods for handling command execution logic
/// based on the specified service method name. Derived controllers can use these methods to implement specific
/// command-handling functionality.</remarks>
/// <param name="commandService"></param>
/// <param name="logger"></param>
public class CommandControllerBase(ICommandService commandService, ILogger logger)
    : ControllerBase
{
    const string PostAsyncMethodName = "PostAsync";
    const string ExecuteAsyncMethodName = "ExecuteAsync";
    const string CommandTypeHeaderName = "X-CommandTypeName";

    readonly ICommandService _commandService = IsArgumentNull.Set(commandService);
    readonly IJsonSerializer _serializer = new NewtonSoftJsonSerializer();
    readonly ILogger _logger = IsArgumentNull.Set(logger);

    /// <summary>
    /// Executes a command asynchronously using the specified method name.
    /// </summary>
    /// <remarks>This method invokes the asynchronous execution of a command. The specific behavior depends on
    /// the implementation of the method identified by the provided method name.</remarks>
    /// <returns></returns>
    protected async Task ExecuteCommandAsync() 
        => await ExecuteCommandAsync(ExecuteAsyncMethodName);

    /// <summary>
    /// Executes the specified command asynchronously and returns the result of the operation.
    /// </summary>
    /// <param name="command">The command to be executed. This cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the result of the command execution,  including a <see cref="Guid"/>
    /// that represents the unique identifier of the operation.</returns>
    protected async Task<ServiceResult<Guid>> ExecuteCommandAsync(ICommand command) 
        => await ExecuteCommandAsync(command, ExecuteAsyncMethodName);

    /// <summary>
    /// Executes a command asynchronously using the specified post method.
    /// </summary>
    /// <remarks>This method invokes the command execution logic with the method name defined by <see
    /// cref="PostAsyncMethodName" />. It is intended to be used as part of the command execution workflow.</remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected async Task PostCommandAsync() 
        => await ExecuteCommandAsync(PostAsyncMethodName);

    /// <summary>
    /// Executes the specified command asynchronously and returns the result of the operation.
    /// </summary>
    /// <param name="command">The command to be executed. Must not be <see langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a  <see cref="ServiceResult{T}"/>
    /// with a <see cref="Guid"/> representing the unique identifier  of the executed command.</returns>
    protected async Task<ServiceResult<Guid>> PostCommandAsync(ICommand command) 
        => await ExecuteCommandAsync(command, PostAsyncMethodName);

    /// <summary>
    /// Executes a command asynchronously by invoking the specified service method.
    /// </summary>
    /// <remarks>This method processes a command received in the HTTP request body. It deserializes the
    /// command,  determines the appropriate bounded context state type, and invokes the specified service method to handle 
    /// the command. The result of the service method is serialized and written to the HTTP response. <para> If an
    /// exception occurs during processing, an error result is serialized and returned with an  HTTP 500 status code.
    /// </para></remarks>
    /// <param name="serviceMethodName">The name of the service method to invoke. This method must be a generic method of type  <see
    /// cref="ICommandService"/> that processes the command.</param>
    /// <returns></returns>
    internal async Task ExecuteCommandAsync(string serviceMethodName)
    {
        var commandName = "???";
        string? content;
        try
        {
            _logger.LogInformationEvent("CommandController", $"Received  command: {Request.Path}");
            var commandTypeName = Request.Headers[CommandTypeHeaderName][0]!;
            var commandType = Type.GetType(commandTypeName)!;
            var sr = new StreamReader(Request.Body);
            var serializedCommand = await sr.ReadToEndAsync();
            var command = (_serializer.Deserialize(serializedCommand, commandType) as ICommand)!;
            commandName = commandType.Name;
            var boundedContextStateType = _commandService.BoundedContextStateTypeMap[command.RouteTo];
            var serviceMethodAsync = (typeof(ICommandService).GetMethod(serviceMethodName))!.MakeGenericMethod(boundedContextStateType);
            var task = (serviceMethodAsync.Invoke(_commandService, [command]) as Task<ServiceResult<Guid>>)!;
            var serviceResult = await task;
            content = _serializer.Serialize(serviceResult);
            Response.StatusCode = (int)HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            var errorResult = new ServiceFailed<Guid>(1456, ex.Message);
            content = _serializer.Serialize(errorResult);
            _logger.LogInformationEvent("CommandController", $"{commandName} {HttpStatusCode.InternalServerError} ({serviceMethodName}) failed due to {ex.Message}");
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        Response.ContentType = _serializer.ContentType;
        await Response.WriteAsync(content);
    }

    /// <summary>
    /// Executes the specified command asynchronously by invoking the corresponding service method.
    /// </summary>
    /// <remarks>This method dynamically resolves the bounded context state type associated with the command's
    /// <c>RouteTo</c> property and invokes the corresponding generic service method. If an exception occurs during
    /// execution, the method returns a failed result encapsulating the error details.</remarks>
    /// <param name="command">The command to be executed. This parameter cannot be null.</param>
    /// <param name="serviceMethodName">The name of the service method to invoke. This must correspond to a valid method in the command service.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the executed command if successful. If the
    /// execution fails, a <see cref="ServiceFailed{T}"/> is returned with the appropriate error code and message.</returns>
    internal async Task<ServiceResult<Guid>> ExecuteCommandAsync(ICommand command, string serviceMethodName)
    {
        try
        {
            _logger.LogInformationEvent("CommandController", $"Received  command: {Request.Path}");
            var boundedContextStateType = _commandService.BoundedContextStateTypeMap[command.RouteTo];
            var serviceMethodAsync = typeof(ICommandService).GetMethod(serviceMethodName)!.MakeGenericMethod(boundedContextStateType);
            var commandTask = (serviceMethodAsync.Invoke(_commandService, [command]) as Task<ServiceResult<Guid>>)!;
            return  await commandTask; 
        }
        catch (Exception ex)
        {
            return  new ServiceFailed<Guid>(command.ErrorCode, ex.Message);
        }
    }

}
