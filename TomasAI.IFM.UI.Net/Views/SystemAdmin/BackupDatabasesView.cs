using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Extensions;
using TomasAI.IFM.ViewModels.SystemAdmin;

namespace TomasAI.IFM.Views.SystemAdmin
{
    public partial class BackupDatabasesView : UserControl, IFormControl
    {
        private BackupDatabasesViewModel _viewModel;

        public BackupDatabasesView(BackupDatabasesViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
        }

        public void Open()
        {
            _viewModel.StartSystemAdminEventConsumer(); 
        }


        public void Close()
        {
            _viewModel.StopSystemAdminEventConsumer();
        }

        void IFormControl.Resize(Control parentControl)
        {
        }

        private void BackupDatabasesView_Load(object sender, EventArgs e)
        {
            _viewModel.OnStatusMessagesUpdate = (databaseName, statusMessage) => this.Post(() =>
            {
                _viewModel.SetStatusMessage(databaseName, statusMessage);
                for (var index = 0; index < clbDatabases.Items.Count; index++)
                {
                    if (clbDatabases.SelectedIndex == index)
                        ShowStatusMessages(databaseName);
                }
            });
            _viewModel.OnDatabaseNamesLoaded = () => this.Post(() => ShowDatabaseNames());
            _viewModel.OnDatabaseBackupComplete = (databaseName) => this.Post(() => CheckBackupComplete(databaseName));
            _viewModel.LoadDatabaseNames();
            SetDefaults();
        }

        private void ShowDatabaseNames()
        {
            clbDatabases.Items.Clear();
            clbDatabases.Enabled = false;
            if (_viewModel.DatabaseNames == null || _viewModel.DatabaseNames.Length == 0) return;
            foreach (var name in _viewModel.DatabaseNames)
                clbDatabases.Items.Add(name);
            clbDatabases.SelectedIndex = 0;
            clbDatabases.Enabled = true;
        }

        private void ShowStatusMessages(string databaseName)
        {
            lbStatusMessages.Items.Clear();
            var statusMessages = _viewModel.GetStatusMessages(databaseName);
            if (statusMessages is null || statusMessages.Length == 0) 
                return;
            foreach (var statusMsg in statusMessages)
                lbStatusMessages.Items.Add(statusMsg);
        }


        private void SetDefaults()
        {
            switch (DateTime.Now.DayOfWeek)
            {
                case DayOfWeek.Monday:
                case DayOfWeek.Tuesday:
                case DayOfWeek.Wednesday:
                case DayOfWeek.Thursday:
                    radDiffBackup.Checked = true;
                    radFullBackup.Checked = false;
                    nudCommandTimeout.Value = 15;
                    break;
                case DayOfWeek.Friday:
                default:
                    radDiffBackup.Checked = false;
                    radFullBackup.Checked = true;
                    nudCommandTimeout.Value = 60;
                    break;
            }
            _viewModel.SetBackupType(radFullBackup.Checked);
        }

        private void CheckBackupComplete(string databaseName)
        {
            for (var index = 0; index < clbDatabases.Items.Count; index++)
            {
                var listItem = clbDatabases.Items[index];
                if ($"{listItem}" == databaseName)
                {
                    //ShowStatusMessages(databaseName);
                    clbDatabases.SetItemChecked(index, false);
                    break;
                }
            }
            if (clbDatabases.CheckedItems.Count == 0)
            {
                btnRun.Enabled = true;
                this.Cursor = Cursors.Default;
                _viewModel.DatabaseBackupCompleted();
            }

        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            btnRun.Enabled = false;
            this.Cursor = Cursors.WaitCursor;
            var databaseNames = new List<string>();
            for (var index = 0; index < clbDatabases.Items.Count; index++)
            {
                var listItem = clbDatabases.Items[index];
                if (clbDatabases.GetItemChecked(index))
                    databaseNames.Add($"{listItem}");
            }
            _viewModel.RunDatabaseBackup(databaseNames);
        }

        private void radDiffBackup_CheckedChanged(object sender, EventArgs e) => _viewModel.SetBackupType(radFullBackup.Checked);

        private void radFullBackup_CheckedChanged(object sender, EventArgs e) => _viewModel.SetBackupType(radFullBackup.Checked);
        
        private void nudCommandTimeout_ValueChanged(object sender, EventArgs e) => _viewModel.SetCommandTimeout(nudCommandTimeout.Value);

        private void clbDatabases_SelectedIndexChanged(object sender, EventArgs e)
        {
            var databaseName = $"{clbDatabases.SelectedItem}";
            ShowStatusMessages(databaseName);
        }

    }
}
