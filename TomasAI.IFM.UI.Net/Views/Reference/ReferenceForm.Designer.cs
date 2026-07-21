namespace TomasAI.IFM.UI.Net.Views.Reference
{
    partial class ReferenceForm
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
            pnlMarketDataSelector = new Panel();
            ddlReferenceDataSelector = new ComboBox();
            lblMarketDataSelector = new Label();
            pnlCommands = new Panel();
            btnImport = new Button();
            btnClose = new Button();
            btnRemove = new Button();
            btnChange = new Button();
            btnAdd = new Button();
            pnlMarketData = new Panel();
            pnlMarketDataSelector.SuspendLayout();
            pnlCommands.SuspendLayout();
            SuspendLayout();
            // 
            // pnlMarketDataSelector
            // 
            pnlMarketDataSelector.BackColor = Color.FromArgb(64, 64, 64);
            pnlMarketDataSelector.Controls.Add(ddlReferenceDataSelector);
            pnlMarketDataSelector.Controls.Add(lblMarketDataSelector);
            pnlMarketDataSelector.Dock = DockStyle.Top;
            pnlMarketDataSelector.Location = new Point(0, 0);
            pnlMarketDataSelector.Margin = new Padding(2);
            pnlMarketDataSelector.Name = "pnlMarketDataSelector";
            pnlMarketDataSelector.Size = new Size(1147, 37);
            pnlMarketDataSelector.TabIndex = 0;
            // 
            // ddlReferenceDataSelector
            // 
            ddlReferenceDataSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlReferenceDataSelector.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlReferenceDataSelector.FormattingEnabled = true;
            ddlReferenceDataSelector.Location = new Point(118, 8);
            ddlReferenceDataSelector.Margin = new Padding(2);
            ddlReferenceDataSelector.Name = "ddlReferenceDataSelector";
            ddlReferenceDataSelector.Size = new Size(908, 24);
            ddlReferenceDataSelector.TabIndex = 1;
            ddlReferenceDataSelector.SelectedIndexChanged += ddlReferenceDataSelector_SelectedIndexChanged;
            // 
            // lblMarketDataSelector
            // 
            lblMarketDataSelector.AutoSize = true;
            lblMarketDataSelector.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblMarketDataSelector.ForeColor = Color.White;
            lblMarketDataSelector.Location = new Point(35, 9);
            lblMarketDataSelector.Margin = new Padding(2, 0, 2, 0);
            lblMarketDataSelector.Name = "lblMarketDataSelector";
            lblMarketDataSelector.Size = new Size(79, 20);
            lblMarketDataSelector.TabIndex = 0;
            lblMarketDataSelector.Text = "Selection:";
            // 
            // pnlCommands
            // 
            pnlCommands.BackColor = Color.FromArgb(64, 64, 64);
            pnlCommands.Controls.Add(btnImport);
            pnlCommands.Controls.Add(btnClose);
            pnlCommands.Controls.Add(btnRemove);
            pnlCommands.Controls.Add(btnChange);
            pnlCommands.Controls.Add(btnAdd);
            pnlCommands.Dock = DockStyle.Right;
            pnlCommands.Location = new Point(1032, 37);
            pnlCommands.Margin = new Padding(2);
            pnlCommands.Name = "pnlCommands";
            pnlCommands.Size = new Size(115, 407);
            pnlCommands.TabIndex = 1;
            // 
            // btnImport
            // 
            btnImport.Enabled = false;
            btnImport.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnImport.ForeColor = Color.Black;
            btnImport.Location = new Point(7, 91);
            btnImport.Margin = new Padding(2);
            btnImport.Name = "btnImport";
            btnImport.Size = new Size(99, 26);
            btnImport.TabIndex = 4;
            btnImport.Text = "&Import";
            btnImport.UseVisualStyleBackColor = true;
            btnImport.Click += btnImport_Click;
            // 
            // btnClose
            // 
            btnClose.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnClose.ForeColor = Color.Black;
            btnClose.Location = new Point(7, 123);
            btnClose.Margin = new Padding(2);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(99, 26);
            btnClose.TabIndex = 3;
            btnClose.Text = "&Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // btnRemove
            // 
            btnRemove.Enabled = false;
            btnRemove.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnRemove.ForeColor = Color.Black;
            btnRemove.Location = new Point(7, 60);
            btnRemove.Margin = new Padding(2);
            btnRemove.Name = "btnRemove";
            btnRemove.Size = new Size(99, 26);
            btnRemove.TabIndex = 2;
            btnRemove.Text = "&Remove";
            btnRemove.UseVisualStyleBackColor = true;
            btnRemove.Click += btnRemove_Click;
            // 
            // btnChange
            // 
            btnChange.Enabled = false;
            btnChange.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnChange.ForeColor = Color.Black;
            btnChange.Location = new Point(7, 32);
            btnChange.Margin = new Padding(2);
            btnChange.Name = "btnChange";
            btnChange.Size = new Size(99, 26);
            btnChange.TabIndex = 1;
            btnChange.Text = "C&hange";
            btnChange.UseVisualStyleBackColor = true;
            btnChange.Click += btnChange_Click;
            // 
            // btnAdd
            // 
            btnAdd.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnAdd.ForeColor = Color.Black;
            btnAdd.Location = new Point(7, 4);
            btnAdd.Margin = new Padding(2);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(99, 26);
            btnAdd.TabIndex = 0;
            btnAdd.Text = "&Add";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAdd_Click;
            // 
            // pnlMarketData
            // 
            pnlMarketData.BackColor = Color.FromArgb(64, 64, 64);
            pnlMarketData.BorderStyle = BorderStyle.FixedSingle;
            pnlMarketData.Dock = DockStyle.Fill;
            pnlMarketData.Location = new Point(0, 37);
            pnlMarketData.Margin = new Padding(2);
            pnlMarketData.Name = "pnlMarketData";
            pnlMarketData.Size = new Size(1032, 407);
            pnlMarketData.TabIndex = 2;
            // 
            // ReferenceForm
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ClientSize = new Size(1147, 444);
            Controls.Add(pnlMarketData);
            Controls.Add(pnlCommands);
            Controls.Add(pnlMarketDataSelector);
            Font = new Font("Microsoft Sans Serif", 7.8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(2);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ReferenceForm";
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Reference Data Manager";
            FormClosing += ReferenceForm_FormClosing;
            Load += ReferenceForm_Load;
            pnlMarketDataSelector.ResumeLayout(false);
            pnlMarketDataSelector.PerformLayout();
            pnlCommands.ResumeLayout(false);
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlMarketDataSelector;
        private System.Windows.Forms.ComboBox ddlReferenceDataSelector;
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