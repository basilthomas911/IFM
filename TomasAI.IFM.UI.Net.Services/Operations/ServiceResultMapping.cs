using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.Services.Operations;

/// <summary>Maps backend service outcomes into transport-neutral presentation outcomes.</summary>
internal static class ServiceResultMapping
{
    /// <summary>Maps a backend result and successful value into a UI operation result.</summary>
    public static UiOperationResult<TUi> ToUiResult<TBackend, TUi>(
        this ServiceResult<TBackend> result,
        Func<TBackend, TUi> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);
        return result.Success && result.Value is not null
            ? UiOperationResult<TUi>.Success(map(result.Value))
            : UiOperationResult<TUi>.Failure(result.ErrorCode, result.ErrorMessage);
    }
}
