namespace TomasAI.IFM.Views.MarketData
{
    partial class MarketDataForm
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
            this.ddlMarketDataSelector = new System.Windows.Forms.ComboBox();
            this.lblMarketDataSelector = new System.Windows.Forms.Label();
            this.pnlCommands = new System.Windows.Forms.Panel();
            this.btnImport = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnChange = new System.Windows.Forms.Button();
            this.btnAdd = new System.Windows.Forms.Button();
            this.pnlMarketData = new System.Windows.Forms.Panel();
            this.pnlMarketDataSelector.SuspendLayout();
            this.pnlCommands.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlMarketDataSelector
            // 
            this.pnlMarketDataSelector.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlMarketDataSelector.Controls.Add(this.ddlMarketDataSelector);
            this.pnlMarketDataSelector.Controls.Add(this.lblMarketDataSelector);
            this.pnlMarketDataSelector.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlMarketDataSelector.Location = new System.Drawing.Point(0, 0);
            this.pnlMarketDataSelector.Margin = new System.Windows.Forms.Padding(2);
            this.pnlMarketDataSelector.Name = "pnlMarketDataSelector";
            this.pnlMarketDataSelector.Size = new System.Drawing.Size(1058, 37);
            this.pnlMarketDataSelector.TabIndex = 0;
            // 
            // ddlMarketDataSelector
            // 
            this.ddlMarketDataSelector.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlMarketDataSelector.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlMarketDataSelector.FormattingEnabled = true;
            this.ddlMarketDataSelector.Location = new System.Drawing.Point(118, 8);
            this.ddlMarketDataSelector.Margin = new System.Windows.Forms.Padding(2);
            this.ddlMarketDataSelector.Name = "ddlMarketDataSelector";
            this.ddlMarketDataSelector.Size = new System.Drawing.Size(826, 24);
            this.ddlMarketDataSelector.TabIndex = 1;
            this.ddlMarketDataSelector.SelectedIndexChanged += new System.EventHandler(this.ddlMarketDataSelector_SelectedIndexChanged);
            // 
            // lblMarketDataSelector
            // 
            this.lblMarketDataSelector.AutoSize = true;
            this.lblMarketDataSelector.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMarketDataSelector.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblMarketDataSelector.Location = new System.Drawing.Point(35, 9);
            this.lblMarketDataSelector.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblMarketDataSelector.Name = "lblMarketDataSelector";
            this.lblMarketDataSelector.Size = new System.Drawing.Size(79, 20);
            this.lblMarketDataSelector.TabIndex = 0;
            this.lblMarketDataSelector.Text = "Selection:";
            // 
            // pnlCommands
            // 
            this.pnlCommands.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlCommands.Controls.Add(this.btnImport);
            this.pnlCommands.Controls.Add(this.btnClose);
            this.pnlCommands.Controls.Add(this.btnRemove);
            this.pnlCommands.Controls.Add(this.btnChange);
            this.pnlCommands.Controls.Add(this.btnAdd);
            this.pnlCommands.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlCommands.Location = new System.Drawing.Point(943, 37);
            this.pnlCommands.Margin = new System.Windows.Forms.Padding(2);
            this.pnlCommands.Name = "pnlCommands";
            this.pnlCommands.Size = new System.Drawing.Size(115, 407);
            this.pnlCommands.TabIndex = 1;
            // 
            // btnImport
            // 
            this.btnImport.Enabled = false;
            this.btnImport.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnImport.Location = new System.Drawing.Point(7, 91);
            this.btnImport.Name = "btnImport";
            this.btnImport.Size = new System.Drawing.Size(99, 26);
            this.btnImport.TabIndex = 4;
            this.btnImport.Text = "Import";
            this.btnImport.UseVisualStyleBackColor = true;
            this.btnImport.Click += new System.EventHandler(this.btnImport_Click);
            // 
            // btnClose
            // 
            this.btnClose.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClose.Location = new System.Drawing.Point(7, 123);
            this.btnClose.Margin = new System.Windows.Forms.Padding(2);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(99, 26);
            this.btnClose.TabIndex = 3;
            this.btnClose.Text = "&Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnRemove
            // 
            this.btnRemove.Enabled = false;
            this.btnRemove.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRemove.Location = new System.Drawing.Point(7, 60);
            this.btnRemove.Margin = new System.Windows.Forms.Padding(2);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(99, 26);
            this.btnRemove.TabIndex = 2;
            this.btnRemove.Text = "&Remove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnChange
            // 
            this.btnChange.Enabled = false;
            this.btnChange.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnChange.Location = new System.Drawing.Point(7, 32);
            this.btnChange.Margin = new System.Windows.Forms.Padding(2);
            this.btnChange.Name = "btnChange";
            this.btnChange.Size = new System.Drawing.Size(99, 26);
            this.btnChange.TabIndex = 1;
            this.btnChange.Text = "C&hange";
            this.btnChange.UseVisualStyleBackColor = true;
            this.btnChange.Click += new System.EventHandler(this.btnChange_Click);
            // 
            // btnAdd
            // 
            this.btnAdd.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAdd.Location = new System.Drawing.Point(7, 4);
            this.btnAdd.Margin = new System.Windows.Forms.Padding(2);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(99, 26);
            this.btnAdd.TabIndex = 0;
            this.btnAdd.Text = "&Add";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // pnlMarketData
            // 
            this.pnlMarketData.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlMarketData.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlMarketData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMarketData.Location = new System.Drawing.Point(0, 37);
            this.pnlMarketData.Margin = new System.Windows.Forms.Padding(2);
            this.pnlMarketData.Name = "pnlMarketData";
            this.pnlMarketData.Size = new System.Drawing.Size(943, 407);
            this.pnlMarketData.TabIndex = 2;
            // 
            // MarketDataForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(1058, 444);
            this.Controls.Add(this.pnlMarketData);
            this.Controls.Add(this.pnlCommands);
            this.Controls.Add(this.pnlMarketDataSelector);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MarketDataForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Market Data Manager";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MarketDataForm_FormClosing);
            this.Load += new System.EventHandler(this.MarketDataForm_Load);
            this.pnlMarketDataSelector.ResumeLayout(false);
            this.pnlMarketDataSelector.PerformLayout();
            this.pnlCommands.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlMarketDataSelector;
        private System.Windows.Forms.ComboBox ddlMarketDataSelector;
        private System.Windows.Forms.Label lblMarketDataSelector;
        private System.Windows.Forms.Panel pnlCommands;
        private System.Windows.Forms.Panel pnlMarketData;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnChange;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnImport;
    }
}