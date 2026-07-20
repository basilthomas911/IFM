using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging;

public interface ICommandServiceApi
{
    Task<ServiceResult<Guid>> PostCommandAsync(string uriPath, ICommand command);
    Task<ServiceResult<Guid>> ExecuteCommandAsync(string uriPath, ICommand command);
    Task<ServiceResult<Guid>> ExecuteCommandAsync(string uriPath, ICommandParameter command);

}
