namespace TomasAI.IFM.Views.Reference
{
    partial class LookupTypeEditorView
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
            splitContainer1 = new SplitContainer();
            lstLookupTypeShortCodes = new ListBox();
            panel1 = new Panel();
            lblShortCodes = new Label();
            lstLookupTypeNames = new ListBox();
            panel2 = new Panel();
            lblLookupTypeNames = new Label();
            tlpLookupTypes = new TableLayoutPanel();
            pnlLookupTypeName = new Panel();
            lblLookupTypeName = new Label();
            pnlShortCode = new Panel();
            lblShortCode = new Label();
            pnlOrderId = new Panel();
            lblOrderId = new Label();
            pnlDescription = new Panel();
            lblDescription = new Label();
            txtLookupTypeName = new TextBox();
            txtShortCode = new TextBox();
            txtOrderId = new TextBox();
            txtDescription = new TextBox();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            panel1.SuspendLayout();
            panel2.SuspendLayout();
            tlpLookupTypes.SuspendLayout();
            pnlLookupTypeName.SuspendLayout();
            pnlShortCode.SuspendLayout();
            pnlOrderId.SuspendLayout();
            pnlDescription.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Margin = new Padding(4, 3, 4, 3);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.BackColor = Color.FromArgb(64, 64, 64);
            splitContainer1.Panel1.Controls.Add(lstLookupTypeShortCodes);
            splitContainer1.Panel1.Controls.Add(panel1);
            splitContainer1.Panel1.Controls.Add(lstLookupTypeNames);
            splitContainer1.Panel1.Controls.Add(panel2);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.BackColor = Color.FromArgb(64, 64, 64);
            splitContainer1.Panel2.Controls.Add(tlpLookupTypes);
            splitContainer1.Size = new Size(1031, 468);
            splitContainer1.SplitterDistance = 454;
            splitContainer1.SplitterWidth = 5;
            splitContainer1.TabIndex = 0;
            // 
            // lstLookupTypeShortCodes
            // 
            lstLookupTypeShortCodes.BackColor = Color.FromArgb(64, 64, 64);
            lstLookupTypeShortCodes.BorderStyle = BorderStyle.FixedSingle;
            lstLookupTypeShortCodes.Dock = DockStyle.Fill;
            lstLookupTypeShortCodes.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lstLookupTypeShortCodes.ForeColor = Color.White;
            lstLookupTypeShortCodes.FormattingEnabled = true;
            lstLookupTypeShortCodes.Location = new Point(0, 270);
            lstLookupTypeShortCodes.Margin = new Padding(4, 3, 4, 3);
            lstLookupTypeShortCodes.Name = "lstLookupTypeShortCodes";
            lstLookupTypeShortCodes.Size = new Size(454, 198);
            lstLookupTypeShortCodes.TabIndex = 10;
            lstLookupTypeShortCodes.SelectedIndexChanged += lstLookupTypeShortCodes_SelectedIndexChanged;
            // 
            // panel1
            // 
            panel1.BackColor = Color.Gray;
            panel1.Controls.Add(lblShortCodes);
            panel1.Dock = DockStyle.Top;
            panel1.Location = new Point(0, 240);
            panel1.Margin = new Padding(4, 3, 4, 3);
            panel1.Name = "panel1";
            panel1.Size = new Size(454, 30);
            panel1.TabIndex = 9;
            // 
            // lblShortCodes
            // 
            lblShortCodes.AutoSize = true;
            lblShortCodes.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblShortCodes.ForeColor = Color.Black;
            lblShortCodes.Location = new Point(180, 5);
            lblShortCodes.Margin = new Padding(4, 0, 4, 0);
            lblShortCodes.Name = "lblShortCodes";
            lblShortCodes.Size = new Size(86, 17);
            lblShortCodes.TabIndex = 0;
            lblShortCodes.Text = "Short Codes";
            lblShortCodes.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lstLookupTypeNames
            // 
            lstLookupTypeNames.BackColor = Color.FromArgb(64, 64, 64);
            lstLookupTypeNames.BorderStyle = BorderStyle.FixedSingle;
            lstLookupTypeNames.Dock = DockStyle.Top;
            lstLookupTypeNames.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lstLookupTypeNames.ForeColor = Color.White;
            lstLookupTypeNames.FormattingEnabled = true;
            lstLookupTypeNames.Location = new Point(0, 30);
            lstLookupTypeNames.Margin = new Padding(4, 3, 4, 3);
            lstLookupTypeNames.Name = "lstLookupTypeNames";
            lstLookupTypeNames.Size = new Size(454, 210);
            lstLookupTypeNames.TabIndex = 7;
            lstLookupTypeNames.SelectedIndexChanged += lstLookupTypeNames_SelectedIndexChanged;
            // 
            // panel2
            // 
            panel2.BackColor = Color.Gray;
            panel2.Controls.Add(lblLookupTypeNames);
            panel2.Dock = DockStyle.Top;
            panel2.Location = new Point(0, 0);
            panel2.Margin = new Padding(4, 3, 4, 3);
            panel2.Name = "panel2";
            panel2.Size = new Size(454, 30);
            panel2.TabIndex = 6;
            // 
            // lblLookupTypeNames
            // 
            lblLookupTypeNames.AutoSize = true;
            lblLookupTypeNames.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblLookupTypeNames.Location = new Point(145, 3);
            lblLookupTypeNames.Margin = new Padding(4, 0, 4, 0);
            lblLookupTypeNames.Name = "lblLookupTypeNames";
            lblLookupTypeNames.Size = new Size(139, 17);
            lblLookupTypeNames.TabIndex = 0;
            lblLookupTypeNames.Text = "Lookup Type Names";
            // 
            // tlpLookupTypes
            // 
            tlpLookupTypes.ColumnCount = 2;
            tlpLookupTypes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31.42857F));
            tlpLookupTypes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68.57143F));
            tlpLookupTypes.Controls.Add(pnlLookupTypeName, 0, 0);
            tlpLookupTypes.Controls.Add(pnlShortCode, 0, 1);
            tlpLookupTypes.Controls.Add(pnlOrderId, 0, 2);
            tlpLookupTypes.Controls.Add(pnlDescription, 0, 3);
            tlpLookupTypes.Controls.Add(txtLookupTypeName, 1, 0);
            tlpLookupTypes.Controls.Add(txtShortCode, 1, 1);
            tlpLookupTypes.Controls.Add(txtOrderId, 1, 2);
            tlpLookupTypes.Controls.Add(txtDescription, 1, 3);
            tlpLookupTypes.Dock = DockStyle.Fill;
            tlpLookupTypes.Location = new Point(0, 0);
            tlpLookupTypes.Margin = new Padding(4, 3, 4, 3);
            tlpLookupTypes.Name = "tlpLookupTypes";
            tlpLookupTypes.RowCount = 5;
            tlpLookupTypes.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tlpLookupTypes.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tlpLookupTypes.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tlpLookupTypes.RowStyles.Add(new RowStyle(SizeType.Absolute, 81F));
            tlpLookupTypes.RowStyles.Add(new RowStyle(SizeType.Absolute, 23F));
            tlpLookupTypes.Size = new Size(572, 468);
            tlpLookupTypes.TabIndex = 0;
            // 
            // pnlLookupTypeName
            // 
            pnlLookupTypeName.Controls.Add(lblLookupTypeName);
            pnlLookupTypeName.Dock = DockStyle.Fill;
            pnlLookupTypeName.Location = new Point(4, 3);
            pnlLookupTypeName.Margin = new Padding(4, 3, 4, 3);
            pnlLookupTypeName.Name = "pnlLookupTypeName";
            pnlLookupTypeName.Size = new Size(171, 29);
            pnlLookupTypeName.TabIndex = 0;
            // 
            // lblLookupTypeName
            // 
            lblLookupTypeName.AutoSize = true;
            lblLookupTypeName.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblLookupTypeName.ForeColor = Color.White;
            lblLookupTypeName.Location = new Point(10, 3);
            lblLookupTypeName.Margin = new Padding(4, 0, 4, 0);
            lblLookupTypeName.Name = "lblLookupTypeName";
            lblLookupTypeName.Size = new Size(136, 17);
            lblLookupTypeName.TabIndex = 0;
            lblLookupTypeName.Text = "Lookup Type Name:";
            // 
            // pnlShortCode
            // 
            pnlShortCode.Controls.Add(lblShortCode);
            pnlShortCode.Dock = DockStyle.Fill;
            pnlShortCode.Location = new Point(4, 38);
            pnlShortCode.Margin = new Padding(4, 3, 4, 3);
            pnlShortCode.Name = "pnlShortCode";
            pnlShortCode.Size = new Size(171, 29);
            pnlShortCode.TabIndex = 1;
            // 
            // lblShortCode
            // 
            lblShortCode.AutoSize = true;
            lblShortCode.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblShortCode.ForeColor = Color.White;
            lblShortCode.Location = new Point(72, 3);
            lblShortCode.Margin = new Padding(4, 0, 4, 0);
            lblShortCode.Name = "lblShortCode";
            lblShortCode.Size = new Size(83, 17);
            lblShortCode.TabIndex = 0;
            lblShortCode.Text = "Short Code:";
            // 
            // pnlOrderId
            // 
            pnlOrderId.Controls.Add(lblOrderId);
            pnlOrderId.Dock = DockStyle.Fill;
            pnlOrderId.Location = new Point(4, 73);
            pnlOrderId.Margin = new Padding(4, 3, 4, 3);
            pnlOrderId.Name = "pnlOrderId";
            pnlOrderId.Size = new Size(171, 29);
            pnlOrderId.TabIndex = 2;
            // 
            // lblOrderId
            // 
            lblOrderId.AutoSize = true;
            lblOrderId.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblOrderId.ForeColor = Color.White;
            lblOrderId.Location = new Point(94, 5);
            lblOrderId.Margin = new Padding(4, 0, 4, 0);
            lblOrderId.Name = "lblOrderId";
            lblOrderId.Size = new Size(64, 17);
            lblOrderId.TabIndex = 0;
            lblOrderId.Text = "Order Id:";
            // 
            // pnlDescription
            // 
            pnlDescription.Controls.Add(lblDescription);
            pnlDescription.Dock = DockStyle.Fill;
            pnlDescription.Location = new Point(4, 108);
            pnlDescription.Margin = new Padding(4, 3, 4, 3);
            pnlDescription.Name = "pnlDescription";
            pnlDescription.Size = new Size(171, 75);
            pnlDescription.TabIndex = 3;
            // 
            // lblDescription
            // 
            lblDescription.AutoSize = true;
            lblDescription.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblDescription.ForeColor = Color.White;
            lblDescription.Location = new Point(72, 7);
            lblDescription.Margin = new Padding(4, 0, 4, 0);
            lblDescription.Name = "lblDescription";
            lblDescription.Size = new Size(83, 17);
            lblDescription.TabIndex = 0;
            lblDescription.Text = "Description:";
            // 
            // txtLookupTypeName
            // 
            txtLookupTypeName.BackColor = Color.Black;
            txtLookupTypeName.BorderStyle = BorderStyle.FixedSingle;
            txtLookupTypeName.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtLookupTypeName.ForeColor = Color.White;
            txtLookupTypeName.Location = new Point(183, 3);
            txtLookupTypeName.Margin = new Padding(4, 3, 4, 3);
            txtLookupTypeName.Name = "txtLookupTypeName";
            txtLookupTypeName.ReadOnly = true;
            txtLookupTypeName.Size = new Size(385, 23);
            txtLookupTypeName.TabIndex = 4;
            // 
            // txtShortCode
            // 
            txtShortCode.BackColor = Color.Black;
            txtShortCode.BorderStyle = BorderStyle.FixedSingle;
            txtShortCode.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtShortCode.ForeColor = Color.White;
            txtShortCode.Location = new Point(183, 38);
            txtShortCode.Margin = new Padding(4, 3, 4, 3);
            txtShortCode.Name = "txtShortCode";
            txtShortCode.ReadOnly = true;
            txtShortCode.Size = new Size(385, 23);
            txtShortCode.TabIndex = 5;
            // 
            // txtOrderId
            // 
            txtOrderId.BackColor = Color.Black;
            txtOrderId.BorderStyle = BorderStyle.FixedSingle;
            txtOrderId.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtOrderId.ForeColor = Color.White;
            txtOrderId.Location = new Point(183, 73);
            txtOrderId.Margin = new Padding(4, 3, 4, 3);
            txtOrderId.Name = "txtOrderId";
            txtOrderId.ReadOnly = true;
            txtOrderId.Size = new Size(143, 23);
            txtOrderId.TabIndex = 6;
            // 
            // txtDescription
            // 
            txtDescription.BackColor = Color.Black;
            txtDescription.BorderStyle = BorderStyle.FixedSingle;
            txtDescription.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtDescription.ForeColor = Color.White;
            txtDescription.Location = new Point(183, 108);
            txtDescription.Margin = new Padding(4, 3, 4, 3);
            txtDescription.Multiline = true;
            txtDescription.Name = "txtDescription";
            txtDescription.ReadOnly = true;
            txtDescription.Size = new Size(385, 72);
            txtDescription.TabIndex = 7;
            txtDescription.TextChanged += txtDescription_TextChanged;
            // 
            // LookupTypeEditorView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(splitContainer1);
            Margin = new Padding(4, 3, 4, 3);
            Name = "LookupTypeEditorView";
            Size = new Size(1031, 468);
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            tlpLookupTypes.ResumeLayout(false);
            tlpLookupTypes.PerformLayout();
            pnlLookupTypeName.ResumeLayout(false);
            pnlLookupTypeName.PerformLayout();
            pnlShortCode.ResumeLayout(false);
            pnlShortCode.PerformLayout();
            pnlOrderId.ResumeLayout(false);
            pnlOrderId.PerformLayout();
            pnlDescription.ResumeLayout(false);
            pnlDescription.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tlpLookupTypes;
        private System.Windows.Forms.Panel pnlLookupTypeName;
        private System.Windows.Forms.Label lblLookupTypeName;
        private System.Windows.Forms.Panel pnlShortCode;
        private System.Windows.Forms.Label lblShortCode;
        private System.Windows.Forms.Panel pnlOrderId;
        private System.Windows.Forms.Label lblOrderId;
        private System.Windows.Forms.Panel pnlDescription;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.TextBox txtLookupTypeName;
        private System.Windows.Forms.TextBox txtShortCode;
        private System.Windows.Forms.TextBox txtOrderId;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.ListBox lstLookupTypeShortCodes;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblShortCodes;
        private System.Windows.Forms.ListBox lstLookupTypeNames;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label lblLookupTypeNames;
    }
}
