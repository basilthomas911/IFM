using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Shared.Log;

namespace TomasAI.IFM.Framework.ServerLauncher
{
    public partial class IFMServerConsole : Form
    {
        public IFMServerConsole()
        {
            InitializeComponent();
        }

        public void AddServerLog(ServerLogType serverLogType, string logInfo)
        {
            try
            {
                if (this.Visible)
                    this.Invoke(() => gridServerLog.Rows.Add($"{DateTime.Now:yyyy-MM-dd hh:mm:ss}", $"{serverLogType}", logInfo));
                else
                    gridServerLog.Rows.Add($"{DateTime.Now:yyyy-MM-dd hh:mm:ss}", $"{serverLogType}", logInfo);
            }
            catch { }
        }

        public void Clear()
        {
            gridServerLog.Rows.Clear();
        }

        private void Invoke(Action viewAction) => ((Control)this).Invoke((MethodInvoker)(() => viewAction()));

        private void IFMServerConsole_SizeChanged(object sender, EventArgs e)
        {
            var gridSize = gridServerLog.Size;
            gridSize.Width = this.Width;
            gridSize.Height = this.Height;
        }

    }
}
