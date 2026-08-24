using TomasAI.IFM.UI.Net.Services.Application;

namespace TomasAI.IFM.UI.Net.Views.SystemInfo;

public partial class SystemWaitView : Form
{
    readonly CommandResponseEventService _eventModel;
    readonly CancellationTokenSource _lifetimeCancellation = new();
    readonly Task _waitTask;
    bool _closeComplete;

    public SystemWaitView(CommandResponseEventService eventModel, string waitText)
    {
        InitializeComponent();
        lblWaitInfo.Text = waitText;
        Cursor = Cursors.WaitCursor;
        _eventModel = eventModel;
        _waitTask = WaitAsync(_lifetimeCancellation.Token);
    }

    public void StopWaiting()
    {
        if (!IsDisposed && !Disposing)
            BeginInvoke(Close);
    }

    async Task WaitAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            if (_eventModel.WaitingForCommandResponse)
                continue;
            if (!IsDisposed && !Disposing)
                BeginInvoke(Close);
            return;
        }
    }

    private async void SystemWaitView_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (_closeComplete)
            return;
        e.Cancel = true;
        _lifetimeCancellation.Cancel();
        try
        {
            await _waitTask;
        }
        catch (OperationCanceledException)
        {
        }
        _lifetimeCancellation.Dispose();
        Cursor = Cursors.Default;
        _closeComplete = true;
        Close();
    }
}
