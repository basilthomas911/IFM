namespace TomasAI.IFM.Shared.EventSourcing;
public static class CommandExtensions
{
    public static ServiceResult<GuidResult> UpdateFailed(this ICommand e, string errorMessage)
      => new ServiceFailed<GuidResult>(e.ErrorCode, errorMessage, new GuidResult(e.CommandId));

    public static ServiceResult<GuidResult> UpdatedOk(this ICommand e, Action updateAction)
    {
        updateAction?.Invoke();
        return new ServiceOk<GuidResult>(new GuidResult(e.CommandId));
    }
}

