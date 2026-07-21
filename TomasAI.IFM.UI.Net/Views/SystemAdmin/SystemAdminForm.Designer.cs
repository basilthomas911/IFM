namespace TomasAI.IFM.UI.Net.Views.SystemAdmin
{
    partial class SystemAdminForm
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
            this.pnlMarketDataSelector = new System.Windows.Forms.Panel();
            this.ddlFunctionSelector = new System.Windows.Forms.ComboBox();
            this.lblFunctionSelector = new System.Windows.Forms.Label();
            this.pnlSystemAdmin = new System.Windows.Forms.Panel();
            this.pnlMarketDataSelector.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlMarketDataSelector
            // 
            this.pnlMarketDataSelector.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlMarketDataSelector.Controls.Add(this.ddlFunctionSelector);
            this.pnlMarketDataSelector.Controls.Add(this.lblFunctionSelector);
            this.pnlMarketDataSelector.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlMarketDataSelector.Location = new System.Drawing.Point(0, 0);
            this.pnlMarketDataSelector.Margin = new System.Windows.Forms.Padding(2);
            this.pnlMarketDataSelector.Name = "pnlMarketDataSelector";
            this.pnlMarketDataSelector.Size = new System.Drawing.Size(1293, 37);
            this.pnlMarketDataSelector.TabIndex = 0;
            // 
            // ddlFunctionSelector
            // 
            this.ddlFunctionSelector.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlFunctionSelector.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlFunctionSelector.FormattingEnabled = true;
            this.ddlFunctionSelector.Location = new System.Drawing.Point(187, 7);
            this.ddlFunctionSelector.Margin = new System.Windows.Forms.Padding(2);
            this.ddlFunctionSelector.Name = "ddlFunctionSelector";
            this.ddlFunctionSelector.Size = new System.Drawing.Size(1104, 26);
            this.ddlFunctionSelector.TabIndex = 1;
            this.ddlFunctionSelector.SelectedIndexChanged += new System.EventHandler(this.ddlMarketDataSelector_SelectedIndexChanged);
            // 
            // lblFunctionSelector
            // 
            this.lblFunctionSelector.AutoSize = true;
            this.lblFunctionSelector.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFunctionSelector.ForeColor = System.Drawing.Color.White;
            this.lblFunctionSelector.Location = new System.Drawing.Point(11, 10);
            this.lblFunctionSelector.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblFunctionSelector.Name = "lblFunctionSelector";
            this.lblFunctionSelector.Size = new System.Drawing.Size(172, 18);
            this.lblFunctionSelector.TabIndex = 0;
            this.lblFunctionSelector.Text = "System Admin Function: ";
            // 
            // pnlSystemAdmin
            // 
            this.pnlSystemAdmin.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlSystemAdmin.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlSystemAdmin.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSystemAdmin.Location = new System.Drawing.Point(0, 37);
            this.pnlSystemAdmin.Margin = new System.Windows.Forms.Padding(2);
            this.pnlSystemAdmin.Name = "pnlSystemAdmin";
            this.pnlSystemAdmin.Size = new System.Drawing.Size(1293, 407);
            this.pnlSystemAdmin.TabIndex = 2;
            // 
            // SystemAdminForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(1293, 444);
            this.Controls.Add(this.pnlSystemAdmin);
            this.Controls.Add(this.pnlMarketDataSelector);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SystemAdminForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "System Admin Manager";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.SystemAdminForm_FormClosed);
            this.Load += new System.EventHandler(this.SystemAdminForm_Load);
            this.pnlMarketDataSelector.ResumeLayout(false);
            this.pnlMarketDataSelector.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlMarketDataSelector;
        private System.Windows.Forms.ComboBox ddlFunctionSelector;
        private System.Windows.Forms.Label lblFunctionSelector;
        private System.Windows.Forms.Panel pnlSystemAdmin;
    }
}