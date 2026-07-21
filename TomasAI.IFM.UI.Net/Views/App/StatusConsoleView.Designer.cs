namespace TomasAI.IFM.UI.Net.Views.App
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
            tabConsoles = new TabControl();
            tabPage1 = new TabPage();
            pnlTradeConsole = new Panel();
            lstTradeStatus = new ListView();
            columnHeader1 = new ColumnHeader();
            columnHeader2 = new ColumnHeader();
            pnlTradeLayout = new TableLayoutPanel();
            txtTradeStatus = new TextBox();
            tabPage2 = new TabPage();
            lstStatusConsole = new ListView();
            colStatusTime = new ColumnHeader();
            colStatus = new ColumnHeader();
            tabPage3 = new TabPage();
            lstMDIFwdLossRatio = new ListView();
            colMDI = new ColumnHeader();
            colTrendDirection = new ColumnHeader();
            colTradeType = new ColumnHeader();
            colForwardLossRatio = new ColumnHeader();
            tabConsoles.SuspendLayout();
            tabPage1.SuspendLayout();
            pnlTradeConsole.SuspendLayout();
            pnlTradeLayout.SuspendLayout();
            tabPage2.SuspendLayout();
            tabPage3.SuspendLayout();
            SuspendLayout();
            // 
            // tabConsoles
            // 
            tabConsoles.Controls.Add(tabPage1);
            tabConsoles.Controls.Add(tabPage2);
            tabConsoles.Controls.Add(tabPage3);
            tabConsoles.Dock = DockStyle.Fill;
            tabConsoles.Location = new Point(0, 0);
            tabConsoles.Margin = new Padding(4, 3, 4, 3);
            tabConsoles.Name = "tabConsoles";
            tabConsoles.SelectedIndex = 0;
            tabConsoles.Size = new Size(550, 308);
            tabConsoles.TabIndex = 0;
            tabConsoles.SelectedIndexChanged += tabConsoles_SelectedIndexChanged;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(pnlTradeConsole);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Margin = new Padding(4, 3, 4, 3);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(4, 3, 4, 3);
            tabPage1.Size = new Size(542, 280);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Trade Console";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // pnlTradeConsole
            // 
            pnlTradeConsole.BackColor = Color.FromArgb(64, 64, 64);
            pnlTradeConsole.Controls.Add(lstTradeStatus);
            pnlTradeConsole.Controls.Add(pnlTradeLayout);
            pnlTradeConsole.Dock = DockStyle.Fill;
            pnlTradeConsole.Location = new Point(4, 3);
            pnlTradeConsole.Margin = new Padding(4, 3, 4, 3);
            pnlTradeConsole.Name = "pnlTradeConsole";
            pnlTradeConsole.Size = new Size(534, 274);
            pnlTradeConsole.TabIndex = 0;
            // 
            // lstTradeStatus
            // 
            lstTradeStatus.BackColor = Color.Black;
            lstTradeStatus.BorderStyle = BorderStyle.FixedSingle;
            lstTradeStatus.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2 });
            lstTradeStatus.Dock = DockStyle.Fill;
            lstTradeStatus.ForeColor = Color.White;
            lstTradeStatus.FullRowSelect = true;
            lstTradeStatus.Location = new Point(0, 35);
            lstTradeStatus.Margin = new Padding(4, 3, 4, 3);
            lstTradeStatus.Name = "lstTradeStatus";
            lstTradeStatus.Size = new Size(534, 239);
            lstTradeStatus.TabIndex = 9;
            lstTradeStatus.UseCompatibleStateImageBehavior = false;
            lstTradeStatus.View = View.Details;
            // 
            // columnHeader1
            // 
            columnHeader1.Text = "Status Time";
            columnHeader1.Width = 100;
            // 
            // columnHeader2
            // 
            columnHeader2.Text = "Status";
            columnHeader2.Width = 500;
            // 
            // pnlTradeLayout
            // 
            pnlTradeLayout.BackColor = Color.Black;
            pnlTradeLayout.ColumnCount = 1;
            pnlTradeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlTradeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 23F));
            pnlTradeLayout.Controls.Add(txtTradeStatus, 0, 0);
            pnlTradeLayout.Dock = DockStyle.Top;
            pnlTradeLayout.Location = new Point(0, 0);
            pnlTradeLayout.Margin = new Padding(4, 3, 4, 3);
            pnlTradeLayout.Name = "pnlTradeLayout";
            pnlTradeLayout.RowCount = 1;
            pnlTradeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            pnlTradeLayout.Size = new Size(534, 35);
            pnlTradeLayout.TabIndex = 0;
            // 
            // txtTradeStatus
            // 
            txtTradeStatus.BackColor = Color.Black;
            txtTradeStatus.BorderStyle = BorderStyle.None;
            txtTradeStatus.Dock = DockStyle.Fill;
            txtTradeStatus.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtTradeStatus.ForeColor = Color.White;
            txtTradeStatus.Location = new Point(6, 6);
            txtTradeStatus.Margin = new Padding(6, 6, 4, 0);
            txtTradeStatus.Name = "txtTradeStatus";
            txtTradeStatus.ReadOnly = true;
            txtTradeStatus.Size = new Size(524, 19);
            txtTradeStatus.TabIndex = 2;
            txtTradeStatus.Text = "Ready";
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(lstStatusConsole);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Margin = new Padding(4, 3, 4, 3);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(4, 3, 4, 3);
            tabPage2.Size = new Size(542, 280);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Status Console";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // lstStatusConsole
            // 
            lstStatusConsole.BackColor = Color.Black;
            lstStatusConsole.BorderStyle = BorderStyle.FixedSingle;
            lstStatusConsole.Columns.AddRange(new ColumnHeader[] { colStatusTime, colStatus });
            lstStatusConsole.Dock = DockStyle.Fill;
            lstStatusConsole.ForeColor = Color.White;
            lstStatusConsole.FullRowSelect = true;
            lstStatusConsole.Location = new Point(4, 3);
            lstStatusConsole.Margin = new Padding(4, 3, 4, 3);
            lstStatusConsole.Name = "lstStatusConsole";
            lstStatusConsole.Size = new Size(534, 274);
            lstStatusConsole.TabIndex = 8;
            lstStatusConsole.UseCompatibleStateImageBehavior = false;
            lstStatusConsole.View = View.Details;
            // 
            // colStatusTime
            // 
            colStatusTime.Text = "Status Time";
            colStatusTime.Width = 100;
            // 
            // colStatus
            // 
            colStatus.Text = "Status";
            colStatus.Width = 500;
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(lstMDIFwdLossRatio);
            tabPage3.Location = new Point(4, 24);
            tabPage3.Margin = new Padding(4, 3, 4, 3);
            tabPage3.Name = "tabPage3";
            tabPage3.Size = new Size(542, 280);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "Fwd Loss Ratio Map";
            tabPage3.UseVisualStyleBackColor = true;
            // 
            // lstMDIFwdLossRatio
            // 
            lstMDIFwdLossRatio.BackColor = Color.Black;
            lstMDIFwdLossRatio.BorderStyle = BorderStyle.None;
            lstMDIFwdLossRatio.Columns.AddRange(new ColumnHeader[] { colMDI, colTrendDirection, colTradeType, colForwardLossRatio });
            lstMDIFwdLossRatio.Dock = DockStyle.Fill;
            lstMDIFwdLossRatio.ForeColor = Color.White;
            lstMDIFwdLossRatio.FullRowSelect = true;
            lstMDIFwdLossRatio.Location = new Point(0, 0);
            lstMDIFwdLossRatio.Margin = new Padding(4, 3, 4, 3);
            lstMDIFwdLossRatio.Name = "lstMDIFwdLossRatio";
            lstMDIFwdLossRatio.Size = new Size(542, 280);
            lstMDIFwdLossRatio.TabIndex = 0;
            lstMDIFwdLossRatio.UseCompatibleStateImageBehavior = false;
            lstMDIFwdLossRatio.View = View.Details;
            // 
            // colMDI
            // 
            colMDI.Text = "Select On";
            colMDI.Width = 150;
            // 
            // colTrendDirection
            // 
            colTrendDirection.Text = "Trend Direction";
            colTrendDirection.Width = 140;
            // 
            // colTradeType
            // 
            colTradeType.Text = "Trade Type";
            colTradeType.Width = 140;
            // 
            // colForwardLossRatio
            // 
            colForwardLossRatio.Text = "ForwardLossRatio";
            colForwardLossRatio.TextAlign = HorizontalAlignment.Center;
            colForwardLossRatio.Width = 140;
            // 
            // StatusConsoleView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(tabConsoles);
            Margin = new Padding(4, 3, 4, 3);
            Name = "StatusConsoleView";
            Size = new Size(550, 308);
            tabConsoles.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            pnlTradeConsole.ResumeLayout(false);
            pnlTradeLayout.ResumeLayout(false);
            pnlTradeLayout.PerformLayout();
            tabPage2.ResumeLayout(false);
            tabPage3.ResumeLayout(false);
            ResumeLayout(false);
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
