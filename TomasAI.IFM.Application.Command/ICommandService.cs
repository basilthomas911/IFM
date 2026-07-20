using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Command;

public interface ICommandService
{
    Dictionary<BoundedContextName, Type> BoundedContextStateTypeMap { get; }
    Task<ServiceResult<Guid>> PostAsync<TBoundedContextState>(ICommand command) where TBoundedContextState : IBoundedContextState<TBoundedContextState>;
    Task<ServiceResult<Guid>> ExecuteAsync<TboundedContextState>(ICommand command) where TboundedContextState : IBoundedContextState<TboundedContextState>;
}
