using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Shared.ServiceApi;

public interface IApplicationCommandApi
{
    Task<ServiceResult<Guid>> StartApplicationAsync(DateOnly valueDate);
    Task<ServiceResult<Guid>> ShutdownApplicationAsync(DateOnly valueDate);
}
