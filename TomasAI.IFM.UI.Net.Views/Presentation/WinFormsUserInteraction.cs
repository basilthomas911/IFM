using TomasAI.IFM.UI.Net.Contracts;

namespace TomasAI.IFM.UI.Net.Views.Presentation;

/// <summary>
/// Implements framework-neutral notifications with WinForms message boxes.
/// </summary>
public sealed class WinFormsUserInteraction(IWin32Window? owner = null) : IUserInteraction
{
    readonly IWin32Window? _owner = owner;

    public ValueTask NotifyAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MessageBox.Show(
            _owner,
            notification.Message,
            notification.Title,
            MessageBoxButtons.OK,
            ToIcon(notification.Severity));
        return ValueTask.CompletedTask;
    }

    public ValueTask<UserConfirmationResult> ConfirmAsync(
        UserConfirmation confirmation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = MessageBox.Show(
            _owner,
            confirmation.Message,
            confirmation.Title,
            MessageBoxButtons.YesNo,
            ToIcon(confirmation.Severity));
        return ValueTask.FromResult(
            result == DialogResult.Yes ? UserConfirmationResult.Yes : UserConfirmationResult.No);
    }

    static MessageBoxIcon ToIcon(UserNotificationSeverity severity)
        => severity switch
        {
            UserNotificationSeverity.Warning => MessageBoxIcon.Warning,
            UserNotificationSeverity.Error => MessageBoxIcon.Error,
            UserNotificationSeverity.Question => MessageBoxIcon.Question,
            _ => MessageBoxIcon.Information
        };
}
