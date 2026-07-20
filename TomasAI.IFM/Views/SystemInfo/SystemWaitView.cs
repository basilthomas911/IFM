using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Models;

namespace TomasAI.IFM.Views.SystemInfo
{
    public partial class SystemWaitView : Form
    {
        private readonly Timer _waitTimer;
        private readonly EventModel _eventModel;

        public SystemWaitView(EventModel eventModel, string waitText)
        {
            InitializeComponent();
            lblWaitInfo.Text = waitText;
            Cursor = Cursors.WaitCursor;
            _eventModel = eventModel;
            _waitTimer = new Timer();
            _waitTimer.Interval = 1000;
            _waitTimer.Tick += _waitTimer_Tick;
            _waitTimer.Start();
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
}
