using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Application.ServiceApi;

public interface IApplicationCommandApi
{
    Task<ServiceResult<Guid>> StartApplicationAsync(DateOnly valueDate);
    Task<ServiceResult<Guid>> ShutdownApplicationAsync(DateOnly valueDate);
}
