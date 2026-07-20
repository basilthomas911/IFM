namespace TomasAI.IFM.Views.App
{
    partial class StatusConsoleView
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
            this.tabConsoles = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.pnlTradeConsole = new System.Windows.Forms.Panel();
            this.lstTradeStatus = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.pnlTradeLayout = new System.Windows.Forms.TableLayoutPanel();
            this.txtTradeStatus = new System.Windows.Forms.TextBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.lstStatusConsole = new System.Windows.Forms.ListView();
            this.colStatusTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.lstMDIFwdLossRatio = new System.Windows.Forms.ListView();
            this.colMDI = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTrendDirection = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTradeType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colForwardLossRatio = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tabConsoles.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.pnlTradeConsole.SuspendLayout();
            this.pnlTradeLayout.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabConsoles
            // 
            this.tabConsoles.Controls.Add(this.tabPage1);
            this.tabConsoles.Controls.Add(this.tabPage2);
            this.tabConsoles.Controls.Add(this.tabPage3);
            this.tabConsoles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabConsoles.Location = new System.Drawing.Point(0, 0);
            this.tabConsoles.Name = "tabConsoles";
            this.tabConsoles.SelectedIndex = 0;
            this.tabConsoles.Size = new System.Drawing.Size(471, 267);
            this.tabConsoles.TabIndex = 0;
            this.tabConsoles.SelectedIndexChanged += new System.EventHandler(this.tabConsoles_SelectedIndexChanged);
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.pnlTradeConsole);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(463, 241);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Trade Console";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // pnlTradeConsole
            // 
            this.pnlTradeConsole.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlTradeConsole.Controls.Add(this.lstTradeStatus);
            this.pnlTradeConsole.Controls.Add(this.pnlTradeLayout);
            this.pnlTradeConsole.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlTradeConsole.Location = new System.Drawing.Point(3, 3);
            this.pnlTradeConsole.Name = "pnlTradeConsole";
            this.pnlTradeConsole.Size = new System.Drawing.Size(457, 235);
            this.pnlTradeConsole.TabIndex = 0;
            // 
            // lstTradeStatus
            // 
            this.lstTradeStatus.BackColor = System.Drawing.Color.Black;
            this.lstTradeStatus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lstTradeStatus.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
            this.lstTradeStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstTradeStatus.ForeColor = System.Drawing.Color.White;
            this.lstTradeStatus.FullRowSelect = true;
            this.lstTradeStatus.HideSelection = false;
            this.lstTradeStatus.Location = new System.Drawing.Point(0, 30);
            this.lstTradeStatus.Name = "lstTradeStatus";
            this.lstTradeStatus.Size = new System.Drawing.Size(457, 205);
            this.lstTradeStatus.TabIndex = 9;
            this.lstTradeStatus.UseCompatibleStateImageBehavior = false;
            this.lstTradeStatus.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Status Time";
            this.columnHeader1.Width = 100;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Status";
            this.columnHeader2.Width = 500;
            // 
            // pnlTradeLayout
            // 
            this.pnlTradeLayout.BackColor = System.Drawing.Color.Black;
            this.pnlTradeLayout.ColumnCount = 1;
            this.pnlTradeLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.pnlTradeLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.pnlTradeLayout.Controls.Add(this.txtTradeStatus, 0, 0);
            this.pnlTradeLayout.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTradeLayout.Location = new System.Drawing.Point(0, 0);
            this.pnlTradeLayout.Name = "pnlTradeLayout";
            this.pnlTradeLayout.RowCount = 1;
            this.pnlTradeLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.pnlTradeLayout.Size = new System.Drawing.Size(457, 30);
            this.pnlTradeLayout.TabIndex = 0;
            // 
            // txtTradeStatus
            // 
            this.txtTradeStatus.BackColor = System.Drawing.Color.Black;
            this.txtTradeStatus.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtTradeStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTradeStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtTradeStatus.ForeColor = System.Drawing.Color.White;
            this.txtTradeStatus.Location = new System.Drawing.Point(5, 5);
            this.txtTradeStatus.Margin = new System.Windows.Forms.Padding(5, 5, 3, 0);
            this.txtTradeStatus.Name = "txtTradeStatus";
            this.txtTradeStatus.ReadOnly = true;
            this.txtTradeStatus.Size = new System.Drawing.Size(449, 19);
            this.txtTradeStatus.TabIndex = 2;
            this.txtTradeStatus.Text = "Ready";
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.lstStatusConsole);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(463, 241);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Status Console";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // lstStatusConsole
            // 
            this.lstStatusConsole.BackColor = System.Drawing.Color.Black;
            this.lstStatusConsole.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lstStatusConsole.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colStatusTime,
            this.colStatus});
            this.lstStatusConsole.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstStatusConsole.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lstStatusConsole.FullRowSelect = true;
            this.lstStatusConsole.HideSelection = false;
            this.lstStatusConsole.Location = new System.Drawing.Point(3, 3);
            this.lstStatusConsole.Name = "lstStatusConsole";
            this.lstStatusConsole.Size = new System.Drawing.Size(457, 235);
            this.lstStatusConsole.TabIndex = 8;
            this.lstStatusConsole.UseCompatibleStateImageBehavior = false;
            this.lstStatusConsole.View = System.Windows.Forms.View.Details;
            // 
            // colStatusTime
            // 
            this.colStatusTime.Text = "Status Time";
            this.colStatusTime.Width = 100;
            // 
            // colStatus
            // 
            this.colStatus.Text = "Status";
            this.colStatus.Width = 500;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.lstMDIFwdLossRatio);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(463, 241);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Fwd Loss Ratio Map";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // lstMDIFwdLossRatio
            // 
            this.lstMDIFwdLossRatio.BackColor = System.Drawing.Color.Black;
            this.lstMDIFwdLossRatio.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lstMDIFwdLossRatio.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colMDI,
            this.colTrendDirection,
            this.colTradeType,
            this.colForwardLossRatio});
            this.lstMDIFwdLossRatio.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstMDIFwdLossRatio.ForeColor = System.Drawing.Color.White;
            this.lstMDIFwdLossRatio.FullRowSelect = true;
            this.lstMDIFwdLossRatio.HideSelection = false;
            this.lstMDIFwdLossRatio.Location = new System.Drawing.Point(0, 0);
            this.lstMDIFwdLossRatio.Name = "lstMDIFwdLossRatio";
            this.lstMDIFwdLossRatio.Size = new System.Drawing.Size(463, 241);
            this.lstMDIFwdLossRatio.TabIndex = 0;
            this.lstMDIFwdLossRatio.UseCompatibleStateImageBehavior = false;
            this.lstMDIFwdLossRatio.View = System.Windows.Forms.View.Details;
            // 
            // colMDI
            // 
            this.colMDI.Text = "Select On";
            this.colMDI.Width = 150;
            // 
            // colTrendDirection
            // 
            this.colTrendDirection.Text = "Trend Direction";
            this.colTrendDirection.Width = 140;
            // 
            // colTradeType
            // 
            this.colTradeType.Text = "Trade Type";
            this.colTradeType.Width = 140;
            // 
            // colForwardLossRatio
            // 
            this.colForwardLossRatio.Text = "ForwardLossRatio";
            this.colForwardLossRatio.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.colForwardLossRatio.Width = 140;
            // 
            // StatusConsoleView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tabConsoles);
            this.Name = "StatusConsoleView";
            this.Size = new System.Drawing.Size(471, 267);
            this.tabConsoles.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.pnlTradeConsole.ResumeLayout(false);
            this.pnlTradeLayout.ResumeLayout(false);
            this.pnlTradeLayout.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabConsoles;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.ListView lstStatusConsole;
        private System.Windows.Forms.ColumnHeader colStatusTime;
        private System.Windows.Forms.ColumnHeader colStatus;
        private System.Windows.Forms.Panel pnlTradeConsole;
        private System.Windows.Forms.TableLayoutPanel pnlTradeLayout;
        private System.Windows.Forms.ListView lstTradeStatus;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.TextBox txtTradeStatus;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.ListView lstMDIFwdLossRatio;
        private System.Windows.Forms.ColumnHeader colMDI;
        private System.Windows.Forms.ColumnHeader colTrendDirection;
        private System.Windows.Forms.ColumnHeader colTradeType;
        private System.Windows.Forms.ColumnHeader colForwardLossRatio;
    }
}
