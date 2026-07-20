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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lstLookupTypeShortCodes = new System.Windows.Forms.ListBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.lblShortCodes = new System.Windows.Forms.Label();
            this.lstLookupTypeNames = new System.Windows.Forms.ListBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.lblLookupTypeNames = new System.Windows.Forms.Label();
            this.tlpLookupTypes = new System.Windows.Forms.TableLayoutPanel();
            this.pnlLookupTypeName = new System.Windows.Forms.Panel();
            this.lblLookupTypeName = new System.Windows.Forms.Label();
            this.pnlShortCode = new System.Windows.Forms.Panel();
            this.lblShortCode = new System.Windows.Forms.Label();
            this.pnlOrderId = new System.Windows.Forms.Panel();
            this.lblOrderId = new System.Windows.Forms.Label();
            this.pnlDescription = new System.Windows.Forms.Panel();
            this.lblDescription = new System.Windows.Forms.Label();
            this.txtLookupTypeName = new System.Windows.Forms.TextBox();
            this.txtShortCode = new System.Windows.Forms.TextBox();
            this.txtOrderId = new System.Windows.Forms.TextBox();
            this.txtDescription = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.tlpLookupTypes.SuspendLayout();
            this.pnlLookupTypeName.SuspendLayout();
            this.pnlShortCode.SuspendLayout();
            this.pnlOrderId.SuspendLayout();
            this.pnlDescription.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.splitContainer1.Panel1.Controls.Add(this.lstLookupTypeShortCodes);
            this.splitContainer1.Panel1.Controls.Add(this.panel1);
            this.splitContainer1.Panel1.Controls.Add(this.lstLookupTypeNames);
            this.splitContainer1.Panel1.Controls.Add(this.panel2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.splitContainer1.Panel2.Controls.Add(this.tlpLookupTypes);
            this.splitContainer1.Size = new System.Drawing.Size(884, 406);
            this.splitContainer1.SplitterDistance = 390;
            this.splitContainer1.TabIndex = 0;
            // 
            // lstLookupTypeShortCodes
            // 
            this.lstLookupTypeShortCodes.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lstLookupTypeShortCodes.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lstLookupTypeShortCodes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstLookupTypeShortCodes.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstLookupTypeShortCodes.ForeColor = System.Drawing.Color.White;
            this.lstLookupTypeShortCodes.FormattingEnabled = true;
            this.lstLookupTypeShortCodes.ItemHeight = 16;
            this.lstLookupTypeShortCodes.Location = new System.Drawing.Point(0, 246);
            this.lstLookupTypeShortCodes.Name = "lstLookupTypeShortCodes";
            this.lstLookupTypeShortCodes.Size = new System.Drawing.Size(390, 160);
            this.lstLookupTypeShortCodes.TabIndex = 10;
            this.lstLookupTypeShortCodes.SelectedIndexChanged += new System.EventHandler(this.lstLookupTypeShortCodes_SelectedIndexChanged);
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.Gray;
            this.panel1.Controls.Add(this.lblShortCodes);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 220);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(390, 26);
            this.panel1.TabIndex = 9;
            // 
            // lblShortCodes
            // 
            this.lblShortCodes.AutoSize = true;
            this.lblShortCodes.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblShortCodes.ForeColor = System.Drawing.Color.Black;
            this.lblShortCodes.Location = new System.Drawing.Point(154, 4);
            this.lblShortCodes.Name = "lblShortCodes";
            this.lblShortCodes.Size = new System.Drawing.Size(86, 17);
            this.lblShortCodes.TabIndex = 0;
            this.lblShortCodes.Text = "Short Codes";
            this.lblShortCodes.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lstLookupTypeNames
            // 
            this.lstLookupTypeNames.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lstLookupTypeNames.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lstLookupTypeNames.Dock = System.Windows.Forms.DockStyle.Top;
            this.lstLookupTypeNames.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstLookupTypeNames.ForeColor = System.Drawing.Color.White;
            this.lstLookupTypeNames.FormattingEnabled = true;
            this.lstLookupTypeNames.ItemHeight = 16;
            this.lstLookupTypeNames.Location = new System.Drawing.Point(0, 26);
            this.lstLookupTypeNames.Name = "lstLookupTypeNames";
            this.lstLookupTypeNames.Size = new System.Drawing.Size(390, 194);
            this.lstLookupTypeNames.TabIndex = 7;
            this.lstLookupTypeNames.SelectedIndexChanged += new System.EventHandler(this.lstLookupTypeNames_SelectedIndexChanged);
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.Gray;
            this.panel2.Controls.Add(this.lblLookupTypeNames);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(390, 26);
            this.panel2.TabIndex = 6;
            // 
            // lblLookupTypeNames
            // 
            this.lblLookupTypeNames.AutoSize = true;
            this.lblLookupTypeNames.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLookupTypeNames.Location = new System.Drawing.Point(124, 3);
            this.lblLookupTypeNames.Name = "lblLookupTypeNames";
            this.lblLookupTypeNames.Size = new System.Drawing.Size(139, 17);
            this.lblLookupTypeNames.TabIndex = 0;
            this.lblLookupTypeNames.Text = "Lookup Type Names";
            // 
            // tlpLookupTypes
            // 
            this.tlpLookupTypes.ColumnCount = 2;
            this.tlpLookupTypes.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 31.42857F));
            this.tlpLookupTypes.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 68.57143F));
            this.tlpLookupTypes.Controls.Add(this.pnlLookupTypeName, 0, 0);
            this.tlpLookupTypes.Controls.Add(this.pnlShortCode, 0, 1);
            this.tlpLookupTypes.Controls.Add(this.pnlOrderId, 0, 2);
            this.tlpLookupTypes.Controls.Add(this.pnlDescription, 0, 3);
            this.tlpLookupTypes.Controls.Add(this.txtLookupTypeName, 1, 0);
            this.tlpLookupTypes.Controls.Add(this.txtShortCode, 1, 1);
            this.tlpLookupTypes.Controls.Add(this.txtOrderId, 1, 2);
            this.tlpLookupTypes.Controls.Add(this.txtDescription, 1, 3);
            this.tlpLookupTypes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpLookupTypes.Location = new System.Drawing.Point(0, 0);
            this.tlpLookupTypes.Name = "tlpLookupTypes";
            this.tlpLookupTypes.RowCount = 5;
            this.tlpLookupTypes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpLookupTypes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpLookupTypes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpLookupTypes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            this.tlpLookupTypes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tlpLookupTypes.Size = new System.Drawing.Size(490, 406);
            this.tlpLookupTypes.TabIndex = 0;
            // 
            // pnlLookupTypeName
            // 
            this.pnlLookupTypeName.Controls.Add(this.lblLookupTypeName);
            this.pnlLookupTypeName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLookupTypeName.Location = new System.Drawing.Point(3, 3);
            this.pnlLookupTypeName.Name = "pnlLookupTypeName";
            this.pnlLookupTypeName.Size = new System.Drawing.Size(147, 24);
            this.pnlLookupTypeName.TabIndex = 0;
            // 
            // lblLookupTypeName
            // 
            this.lblLookupTypeName.AutoSize = true;
            this.lblLookupTypeName.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLookupTypeName.ForeColor = System.Drawing.Color.White;
            this.lblLookupTypeName.Location = new System.Drawing.Point(9, 3);
            this.lblLookupTypeName.Name = "lblLookupTypeName";
            this.lblLookupTypeName.Size = new System.Drawing.Size(136, 17);
            this.lblLookupTypeName.TabIndex = 0;
            this.lblLookupTypeName.Text = "Lookup Type Name:";
            // 
            // pnlShortCode
            // 
            this.pnlShortCode.Controls.Add(this.lblShortCode);
            this.pnlShortCode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlShortCode.Location = new System.Drawing.Point(3, 33);
            this.pnlShortCode.Name = "pnlShortCode";
            this.pnlShortCode.Size = new System.Drawing.Size(147, 24);
            this.pnlShortCode.TabIndex = 1;
            // 
            // lblShortCode
            // 
            this.lblShortCode.AutoSize = true;
            this.lblShortCode.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblShortCode.ForeColor = System.Drawing.Color.White;
            this.lblShortCode.Location = new System.Drawing.Point(62, 3);
            this.lblShortCode.Name = "lblShortCode";
            this.lblShortCode.Size = new System.Drawing.Size(83, 17);
            this.lblShortCode.TabIndex = 0;
            this.lblShortCode.Text = "Short Code:";
            // 
            // pnlOrderId
            // 
            this.pnlOrderId.Controls.Add(this.lblOrderId);
            this.pnlOrderId.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlOrderId.Location = new System.Drawing.Point(3, 63);
            this.pnlOrderId.Name = "pnlOrderId";
            this.pnlOrderId.Size = new System.Drawing.Size(147, 24);
            this.pnlOrderId.TabIndex = 2;
            // 
            // lblOrderId
            // 
            this.lblOrderId.AutoSize = true;
            this.lblOrderId.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblOrderId.ForeColor = System.Drawing.Color.White;
            this.lblOrderId.Location = new System.Drawing.Point(81, 4);
            this.lblOrderId.Name = "lblOrderId";
            this.lblOrderId.Size = new System.Drawing.Size(64, 17);
            this.lblOrderId.TabIndex = 0;
            this.lblOrderId.Text = "Order Id:";
            // 
            // pnlDescription
            // 
            this.pnlDescription.Controls.Add(this.lblDescription);
            this.pnlDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlDescription.Location = new System.Drawing.Point(3, 93);
            this.pnlDescription.Name = "pnlDescription";
            this.pnlDescription.Size = new System.Drawing.Size(147, 64);
            this.pnlDescription.TabIndex = 3;
            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDescription.ForeColor = System.Drawing.Color.White;
            this.lblDescription.Location = new System.Drawing.Point(62, 6);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(83, 17);
            this.lblDescription.TabIndex = 0;
            this.lblDescription.Text = "Description:";
            // 
            // txtLookupTypeName
            // 
            this.txtLookupTypeName.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtLookupTypeName.Location = new System.Drawing.Point(156, 3);
            this.txtLookupTypeName.Name = "txtLookupTypeName";
            this.txtLookupTypeName.Size = new System.Drawing.Size(331, 23);
            this.txtLookupTypeName.TabIndex = 4;
            // 
            // txtShortCode
            // 
            this.txtShortCode.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtShortCode.Location = new System.Drawing.Point(156, 33);
            this.txtShortCode.Name = "txtShortCode";
            this.txtShortCode.Size = new System.Drawing.Size(331, 23);
            this.txtShortCode.TabIndex = 5;
            // 
            // txtOrderId
            // 
            this.txtOrderId.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtOrderId.Location = new System.Drawing.Point(156, 63);
            this.txtOrderId.Name = "txtOrderId";
            this.txtOrderId.Size = new System.Drawing.Size(123, 23);
            this.txtOrderId.TabIndex = 6;
            // 
            // txtDescription
            // 
            this.txtDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDescription.Location = new System.Drawing.Point(156, 93);
            this.txtDescription.Multiline = true;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.Size = new System.Drawing.Size(331, 63);
            this.txtDescription.TabIndex = 7;
            // 
            // LookupTypeEditorView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer1);
            this.Name = "LookupTypeEditorView";
            this.Size = new System.Drawing.Size(884, 406);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.tlpLookupTypes.ResumeLayout(false);
            this.tlpLookupTypes.PerformLayout();
            this.pnlLookupTypeName.ResumeLayout(false);
            this.pnlLookupTypeName.PerformLayout();
            this.pnlShortCode.ResumeLayout(false);
            this.pnlShortCode.PerformLayout();
            this.pnlOrderId.ResumeLayout(false);
            this.pnlOrderId.PerformLayout();
            this.pnlDescription.ResumeLayout(false);
            this.pnlDescription.PerformLayout();
            this.ResumeLayout(false);

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
