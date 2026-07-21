using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Views.SystemInfo;

public partial class SystemWaitView : Form
{
    readonly  System.Threading.Timer _waitTimer;
    readonly EventModel _eventModel;

    public SystemWaitView(EventModel eventModel, string waitText)
    {
        InitializeComponent();
        lblWaitInfo.Text = waitText;
        Cursor = Cursors.WaitCursor;
        _eventModel = eventModel;
        TimerCallback timerCallback = o => _waitTimer_Tick(this, EventArgs.Empty);
        _waitTimer = new System.Threading.Timer(timerCallback, default, 0,1000);
    }

    public void StopWaiting()
    {
    }

    private void _waitTimer_Tick(object sender, EventArgs e)
    {
        if (!_eventModel.WaitingForCommandResponse)
            this.Close();
    }

    private void SystemWaitView_FormClosed(object sender, FormClosedEventArgs e)
    {
        Cursor = Cursors.Default;
    }
}
