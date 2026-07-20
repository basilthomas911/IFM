namespace TomasAI.IFM.Framework.ServerLauncher
{
    partial class IFMServerConsole
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.gridServerLog = new System.Windows.Forms.DataGridView();
            this.colLogDate = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLogType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLogInfo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.gridServerLog)).BeginInit();
            this.SuspendLayout();
            // 
            // gridServerLog
            // 
            this.gridServerLog.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridServerLog.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colLogDate,
            this.colLogType,
            this.colLogInfo});
            this.gridServerLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridServerLog.Location = new System.Drawing.Point(0, 0);
            this.gridServerLog.Margin = new System.Windows.Forms.Padding(2);
            this.gridServerLog.Name = "gridServerLog";
            this.gridServerLog.RowTemplate.Height = 24;
            this.gridServerLog.Size = new System.Drawing.Size(1037, 617);
            this.gridServerLog.TabIndex = 2;
            // 
            // colLogDate
            // 
            this.colLogDate.DataPropertyName = "LogDate";
            this.colLogDate.HeaderText = "Log Date";
            this.colLogDate.Name = "colLogDate";
            this.colLogDate.Width = 120;
            // 
            // colLogType
            // 
            this.colLogType.DataPropertyName = "LogType";
            this.colLogType.HeaderText = "LogType";
            this.colLogType.Name = "colLogType";
            this.colLogType.ReadOnly = true;
            // 
            // colLogInfo
            // 
            this.colLogInfo.HeaderText = "Log Info";
            this.colLogInfo.Name = "colLogInfo";
            this.colLogInfo.ReadOnly = true;
            this.colLogInfo.Width = 1000;
            // 
            // IFMServerConsole
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1037, 617);
            this.Controls.Add(this.gridServerLog);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.MaximizeBox = false;
            this.Name = "IFMServerConsole";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "IFM Server Console";
            this.SizeChanged += new System.EventHandler(this.IFMServerConsole_SizeChanged);
            ((System.ComponentModel.ISupportInitialize)(this.gridServerLog)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.DataGridView gridServerLog;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLogDate;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLogType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLogInfo;
    }
}

