using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;

namespace TomasAI.IFM.UI.Net.Services.Application;

/// <summary>Read-only UI boundary for the API-owned Application lifecycle.</summary>
public sealed class ApplicationQueryService(IApplicationQueryApi queryApi)
    : UiServiceBase<ApplicationQueryService>
{
    public async Task<ApplicationStartupStatus?> GetStartupStatusAsync()
    {
        var result = await queryApi.GetStartupStatusAsync().ConfigureAwait(false);
        if (result.Success)
            return result.Value;
        RaiseError(result.ErrorCode, result.ErrorMessage);
        return null;
    }
}
