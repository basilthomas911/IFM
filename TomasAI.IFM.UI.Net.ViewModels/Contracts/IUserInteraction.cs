namespace TomasAI.IFM.UI.Net.Contracts;

/// <summary>
/// Provides framework-neutral user notifications and confirmation prompts.
/// </summary>
public interface IUserInteraction
{
    /// <summary>
    /// Displays a notification to the user.
    /// </summary>
    ValueTask NotifyAsync(UserNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a yes-or-no decision from the user.
    /// </summary>
    ValueTask<UserConfirmationResult> ConfirmAsync(
        UserConfirmation confirmation,
        CancellationToken cancellationToken = default);
}

public readonly record struct UserNotification(
    string Message,
    string Title,
    UserNotificationSeverity Severity = UserNotificationSeverity.Information);

public readonly record struct UserConfirmation(
    string Message,
    string Title,
    UserNotificationSeverity Severity = UserNotificationSeverity.Question);

public enum UserNotificationSeverity
{
    Information,
    Warning,
    Error,
    Question
}

public enum UserConfirmationResult
{
    No,
    Yes
}
