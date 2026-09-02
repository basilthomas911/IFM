using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Shared.ServiceApi;

public interface IApplicationQueryApi
{
    Task<ServiceResult<ApplicationStartupStatus>> GetStartupStatusAsync();
}
