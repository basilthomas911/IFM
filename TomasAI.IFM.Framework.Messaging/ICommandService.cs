using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging
{
    public interface ICommandService
    {
        Task<ServiceResult<Guid>> PostApiCommandAsync(ICommand command, string controllerName);
        Task<ServiceResult<Guid>> ExecuteApiCommandAsync(ICommand command, string controllerName);
    }
}
