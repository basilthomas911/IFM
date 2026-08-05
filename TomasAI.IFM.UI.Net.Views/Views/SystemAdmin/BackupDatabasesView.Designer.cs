namespace TomasAI.IFM.UI.Net.Views.SystemAdmin
{
    partial class BackupDatabasesView
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pnlBackupDatabases = new System.Windows.Forms.SplitContainer();
            this.clbDatabases = new System.Windows.Forms.CheckedListBox();
            this.pnlBackupStatus = new System.Windows.Forms.Panel();
            this.lbStatusMessages = new System.Windows.Forms.ListBox();
            this.pnlCommands = new System.Windows.Forms.TableLayoutPanel();
            this.pnlBackupType = new System.Windows.Forms.Panel();
            this.lblCommandTimeout = new System.Windows.Forms.Label();
            this.nudCommandTimeout = new System.Windows.Forms.NumericUpDown();
            this.radFullBackup = new System.Windows.Forms.RadioButton();
            this.radDiffBackup = new System.Windows.Forms.RadioButton();
            this.btnRun = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pnlBackupDatabases)).BeginInit();
            this.pnlBackupDatabases.Panel1.SuspendLayout();
            this.pnlBackupDatabases.Panel2.SuspendLayout();
            this.pnlBackupDatabases.SuspendLayout();
            this.pnlBackupStatus.SuspendLayout();
            this.pnlCommands.SuspendLayout();
            this.pnlBackupType.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudCommandTimeout)).BeginInit();
            this.SuspendLayout();
            // 
            // pnlBackupDatabases
            // 
            this.pnlBackupDatabases.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlBackupDatabases.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlBackupDatabases.Location = new System.Drawing.Point(0, 0);
            this.pnlBackupDatabases.Name = "pnlBackupDatabases";
            // 
            // pnlBackupDatabases.Panel1
            // 
            this.pnlBackupDatabases.Panel1.Controls.Add(this.clbDatabases);
            // 
            // pnlBackupDatabases.Panel2
            // 
            this.pnlBackupDatabases.Panel2.Controls.Add(this.pnlBackupStatus);
            this.pnlBackupDatabases.Size = new System.Drawing.Size(1293, 407);
            this.pnlBackupDatabases.SplitterDistance = 267;
            this.pnlBackupDatabases.TabIndex = 0;
            // 
            // clbDatabases
            // 
            this.clbDatabases.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.clbDatabases.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.clbDatabases.Dock = System.Windows.Forms.DockStyle.Top;
            this.clbDatabases.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.clbDatabases.ForeColor = System.Drawing.Color.White;
            this.clbDatabases.FormattingEnabled = true;
            this.clbDatabases.Location = new System.Drawing.Point(0, 0);
            this.clbDatabases.Margin = new System.Windows.Forms.Padding(5, 3, 3, 3);
            this.clbDatabases.Name = "clbDatabases";
            this.clbDatabases.Size = new System.Drawing.Size(267, 403);
            this.clbDatabases.TabIndex = 0;
            this.clbDatabases.SelectedIndexChanged += new System.EventHandler(this.clbDatabases_SelectedIndexChanged);
            // 
            // pnlBackupStatus
            // 
            this.pnlBackupStatus.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlBackupStatus.Controls.Add(this.lbStatusMessages);
            this.pnlBackupStatus.Controls.Add(this.pnlCommands);
            this.pnlBackupStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlBackupStatus.Location = new System.Drawing.Point(0, 0);
            this.pnlBackupStatus.Name = "pnlBackupStatus";
            this.pnlBackupStatus.Size = new System.Drawing.Size(1022, 407);
            this.pnlBackupStatus.TabIndex = 0;
            // 
            // lbStatusMessages
            // 
            this.lbStatusMessages.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lbStatusMessages.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbStatusMessages.ForeColor = System.Drawing.Color.White;
            this.lbStatusMessages.FormattingEnabled = true;
            this.lbStatusMessages.ItemHeight = 18;
            this.lbStatusMessages.Location = new System.Drawing.Point(3, 39);
            this.lbStatusMessages.Name = "lbStatusMessages";
            this.lbStatusMessages.Size = new System.Drawing.Size(1010, 364);
            this.lbStatusMessages.TabIndex = 1;
            // 
            // pnlCommands
            // 
            this.pnlCommands.ColumnCount = 2;
            this.pnlCommands.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.pnlCommands.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.pnlCommands.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.pnlCommands.Controls.Add(this.pnlBackupType, 0, 0);
            this.pnlCommands.Controls.Add(this.btnRun, 1, 0);
            this.pnlCommands.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCommands.Location = new System.Drawing.Point(0, 0);
            this.pnlCommands.Name = "pnlCommands";
            this.pnlCommands.RowCount = 1;
            this.pnlCommands.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.pnlCommands.Size = new System.Drawing.Size(1022, 36);
            this.pnlCommands.TabIndex = 0;
            // 
            // pnlBackupType
            // 
            this.pnlBackupType.Controls.Add(this.lblCommandTimeout);
            this.pnlBackupType.Controls.Add(this.nudCommandTimeout);
            this.pnlBackupType.Controls.Add(this.radFullBackup);
            this.pnlBackupType.Controls.Add(this.radDiffBackup);
            this.pnlBackupType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlBackupType.Location = new System.Drawing.Point(3, 3);
            this.pnlBackupType.Name = "pnlBackupType";
            this.pnlBackupType.Size = new System.Drawing.Size(856, 30);
            this.pnlBackupType.TabIndex = 1;
            // 
            // lblCommandTimeout
            // 
            this.lblCommandTimeout.AutoSize = true;
            this.lblCommandTimeout.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCommandTimeout.ForeColor = System.Drawing.Color.White;
            this.lblCommandTimeout.Location = new System.Drawing.Point(281, 6);
            this.lblCommandTimeout.Name = "lblCommandTimeout";
            this.lblCommandTimeout.Size = new System.Drawing.Size(140, 18);
            this.lblCommandTimeout.TabIndex = 3;
            this.lblCommandTimeout.Text = "Command Timeout:";
            // 
            // nudCommandTimeout
            // 
            this.nudCommandTimeout.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.nudCommandTimeout.Location = new System.Drawing.Point(427, 4);
            this.nudCommandTimeout.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.nudCommandTimeout.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.nudCommandTimeout.Name = "nudCommandTimeout";
            this.nudCommandTimeout.Size = new System.Drawing.Size(57, 24);
            this.nudCommandTimeout.TabIndex = 2;
            this.nudCommandTimeout.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.nudCommandTimeout.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.nudCommandTimeout.ValueChanged += new System.EventHandler(this.nudCommandTimeout_ValueChanged);
            // 
            // radFullBackup
            // 
            this.radFullBackup.AutoSize = true;
            this.radFullBackup.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.radFullBackup.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.radFullBackup.ForeColor = System.Drawing.Color.White;
            this.radFullBackup.Location = new System.Drawing.Point(159, 4);
            this.radFullBackup.Name = "radFullBackup";
            this.radFullBackup.Size = new System.Drawing.Size(103, 22);
            this.radFullBackup.TabIndex = 1;
            this.radFullBackup.Text = "Full Backup";
            this.radFullBackup.UseVisualStyleBackColor = true;
            this.radFullBackup.CheckedChanged += new System.EventHandler(this.radFullBackup_CheckedChanged);
            // 
            // radDiffBackup
            // 
            this.radDiffBackup.AutoSize = true;
            this.radDiffBackup.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.radDiffBackup.Checked = true;
            this.radDiffBackup.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.radDiffBackup.ForeColor = System.Drawing.Color.White;
            this.radDiffBackup.Location = new System.Drawing.Point(4, 4);
            this.radDiffBackup.Name = "radDiffBackup";
            this.radDiffBackup.Size = new System.Drawing.Size(149, 22);
            this.radDiffBackup.TabIndex = 0;
            this.radDiffBackup.TabStop = true;
            this.radDiffBackup.Text = "Differential Backup";
            this.radDiffBackup.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.radDiffBackup.UseVisualStyleBackColor = true;
            this.radDiffBackup.CheckedChanged += new System.EventHandler(this.radDiffBackup_CheckedChanged);
            // 
            // btnRun
            // 
            this.btnRun.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRun.Location = new System.Drawing.Point(865, 3);
            this.btnRun.Name = "btnRun";
            this.btnRun.Size = new System.Drawing.Size(148, 30);
            this.btnRun.TabIndex = 0;
            this.btnRun.Text = "Backup Databases";
            this.btnRun.UseVisualStyleBackColor = true;
            this.btnRun.Click += new System.EventHandler(this.btnRun_Click);
            // 
            // BackupDatabasesView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pnlBackupDatabases);
            this.Name = "BackupDatabasesView";
            this.Size = new System.Drawing.Size(1293, 407);
            this.Load += new System.EventHandler(this.BackupDatabasesView_Load);
            this.pnlBackupDatabases.Panel1.ResumeLayout(false);
            this.pnlBackupDatabases.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pnlBackupDatabases)).EndInit();
            this.pnlBackupDatabases.ResumeLayout(false);
            this.pnlBackupStatus.ResumeLayout(false);
            this.pnlCommands.ResumeLayout(false);
            this.pnlBackupType.ResumeLayout(false);
            this.pnlBackupType.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudCommandTimeout)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer pnlBackupDatabases;
        private System.Windows.Forms.CheckedListBox clbDatabases;
        private System.Windows.Forms.Panel pnlBackupStatus;
        private System.Windows.Forms.TableLayoutPanel pnlCommands;
        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.ListBox lbStatusMessages;
        private System.Windows.Forms.Panel pnlBackupType;
        private System.Windows.Forms.RadioButton radFullBackup;
        private System.Windows.Forms.RadioButton radDiffBackup;
        private System.Windows.Forms.Label lblCommandTimeout;
        private System.Windows.Forms.NumericUpDown nudCommandTimeout;
    }
}
