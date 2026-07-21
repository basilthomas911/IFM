namespace TomasAI.IFM.UI.Net.Views.Trade
{
    partial class CreateFundForm
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
            this.pnlCreateFund = new System.Windows.Forms.TableLayoutPanel();
            this.pnlFundId = new System.Windows.Forms.Panel();
            this.lblFundId = new System.Windows.Forms.Label();
            this.pnlName = new System.Windows.Forms.Panel();
            this.lblFundName = new System.Windows.Forms.Label();
            this.pnlDescription = new System.Windows.Forms.Panel();
            this.lblDescription = new System.Windows.Forms.Label();
            this.pnlInitialBalance = new System.Windows.Forms.Panel();
            this.lblInitialBalance = new System.Windows.Forms.Label();
            this.txtFundId = new System.Windows.Forms.TextBox();
            this.txtFundName = new System.Windows.Forms.TextBox();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.txtInitialBalance = new System.Windows.Forms.MaskedTextBox();
            this.pnlButtons = new System.Windows.Forms.Panel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.pnlCreateFund.SuspendLayout();
            this.pnlFundId.SuspendLayout();
            this.pnlName.SuspendLayout();
            this.pnlDescription.SuspendLayout();
            this.pnlInitialBalance.SuspendLayout();
            this.pnlButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlCreateFund
            // 
            this.pnlCreateFund.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlCreateFund.ColumnCount = 2;
            this.pnlCreateFund.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18.28063F));
            this.pnlCreateFund.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 81.71937F));
            this.pnlCreateFund.Controls.Add(this.pnlFundId, 0, 0);
            this.pnlCreateFund.Controls.Add(this.pnlName, 0, 1);
            this.pnlCreateFund.Controls.Add(this.pnlDescription, 0, 2);
            this.pnlCreateFund.Controls.Add(this.pnlInitialBalance, 0, 3);
            this.pnlCreateFund.Controls.Add(this.txtFundId, 1, 0);
            this.pnlCreateFund.Controls.Add(this.txtFundName, 1, 1);
            this.pnlCreateFund.Controls.Add(this.txtDescription, 1, 2);
            this.pnlCreateFund.Controls.Add(this.txtInitialBalance, 1, 3);
            this.pnlCreateFund.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCreateFund.Location = new System.Drawing.Point(0, 0);
            this.pnlCreateFund.Margin = new System.Windows.Forms.Padding(2);
            this.pnlCreateFund.Name = "pnlCreateFund";
            this.pnlCreateFund.RowCount = 5;
            this.pnlCreateFund.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.pnlCreateFund.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.pnlCreateFund.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.pnlCreateFund.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.pnlCreateFund.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.pnlCreateFund.Size = new System.Drawing.Size(734, 103);
            this.pnlCreateFund.TabIndex = 0;
            // 
            // pnlFundId
            // 
            this.pnlFundId.Controls.Add(this.lblFundId);
            this.pnlFundId.Location = new System.Drawing.Point(2, 2);
            this.pnlFundId.Margin = new System.Windows.Forms.Padding(2);
            this.pnlFundId.Name = "pnlFundId";
            this.pnlFundId.Size = new System.Drawing.Size(129, 21);
            this.pnlFundId.TabIndex = 0;
            // 
            // lblFundId
            // 
            this.lblFundId.AutoSize = true;
            this.lblFundId.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFundId.ForeColor = System.Drawing.Color.White;
            this.lblFundId.Location = new System.Drawing.Point(68, 2);
            this.lblFundId.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblFundId.Name = "lblFundId";
            this.lblFundId.Size = new System.Drawing.Size(59, 17);
            this.lblFundId.TabIndex = 0;
            this.lblFundId.Text = "Fund Id:";
            // 
            // pnlName
            // 
            this.pnlName.Controls.Add(this.lblFundName);
            this.pnlName.Location = new System.Drawing.Point(2, 28);
            this.pnlName.Margin = new System.Windows.Forms.Padding(2);
            this.pnlName.Name = "pnlName";
            this.pnlName.Size = new System.Drawing.Size(129, 21);
            this.pnlName.TabIndex = 1;
            // 
            // lblFundName
            // 
            this.lblFundName.AutoSize = true;
            this.lblFundName.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFundName.ForeColor = System.Drawing.Color.White;
            this.lblFundName.Location = new System.Drawing.Point(42, 1);
            this.lblFundName.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblFundName.Name = "lblFundName";
            this.lblFundName.Size = new System.Drawing.Size(85, 17);
            this.lblFundName.TabIndex = 0;
            this.lblFundName.Text = "Fund Name:";
            // 
            // pnlDescription
            // 
            this.pnlDescription.Controls.Add(this.lblDescription);
            this.pnlDescription.Location = new System.Drawing.Point(2, 54);
            this.pnlDescription.Margin = new System.Windows.Forms.Padding(2);
            this.pnlDescription.Name = "pnlDescription";
            this.pnlDescription.Size = new System.Drawing.Size(129, 21);
            this.pnlDescription.TabIndex = 2;
            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDescription.ForeColor = System.Drawing.Color.White;
            this.lblDescription.Location = new System.Drawing.Point(44, 1);
            this.lblDescription.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(83, 17);
            this.lblDescription.TabIndex = 0;
            this.lblDescription.Text = "Description:";
            // 
            // pnlInitialBalance
            // 
            this.pnlInitialBalance.Controls.Add(this.lblInitialBalance);
            this.pnlInitialBalance.Location = new System.Drawing.Point(2, 80);
            this.pnlInitialBalance.Margin = new System.Windows.Forms.Padding(2);
            this.pnlInitialBalance.Name = "pnlInitialBalance";
            this.pnlInitialBalance.Size = new System.Drawing.Size(129, 21);
            this.pnlInitialBalance.TabIndex = 3;
            // 
            // lblInitialBalance
            // 
            this.lblInitialBalance.AutoSize = true;
            this.lblInitialBalance.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblInitialBalance.ForeColor = System.Drawing.Color.White;
            this.lblInitialBalance.Location = new System.Drawing.Point(28, 2);
            this.lblInitialBalance.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblInitialBalance.Name = "lblInitialBalance";
            this.lblInitialBalance.Size = new System.Drawing.Size(99, 17);
            this.lblInitialBalance.TabIndex = 0;
            this.lblInitialBalance.Text = "Initial Balance:";
            // 
            // txtFundId
            // 
            this.txtFundId.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtFundId.Location = new System.Drawing.Point(136, 2);
            this.txtFundId.Margin = new System.Windows.Forms.Padding(2);
            this.txtFundId.Name = "txtFundId";
            this.txtFundId.ReadOnly = true;
            this.txtFundId.Size = new System.Drawing.Size(147, 23);
            this.txtFundId.TabIndex = 4;
            // 
            // txtFundName
            // 
            this.txtFundName.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtFundName.Location = new System.Drawing.Point(136, 28);
            this.txtFundName.Margin = new System.Windows.Forms.Padding(2);
            this.txtFundName.Name = "txtFundName";
            this.txtFundName.Size = new System.Drawing.Size(596, 23);
            this.txtFundName.TabIndex = 5;
            // 
            // txtDescription
            // 
            this.txtDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDescription.Location = new System.Drawing.Point(136, 54);
            this.txtDescription.Margin = new System.Windows.Forms.Padding(2);
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.Size = new System.Drawing.Size(596, 23);
            this.txtDescription.TabIndex = 6;
            // 
            // txtInitialBalance
            // 
            this.txtInitialBalance.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtInitialBalance.Location = new System.Drawing.Point(136, 80);
            this.txtInitialBalance.Margin = new System.Windows.Forms.Padding(2);
            this.txtInitialBalance.Name = "txtInitialBalance";
            this.txtInitialBalance.Size = new System.Drawing.Size(147, 23);
            this.txtInitialBalance.TabIndex = 7;
            // 
            // pnlButtons
            // 
            this.pnlButtons.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlButtons.Controls.Add(this.btnCancel);
            this.pnlButtons.Controls.Add(this.btnSave);
            this.pnlButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlButtons.Location = new System.Drawing.Point(0, 108);
            this.pnlButtons.Margin = new System.Windows.Forms.Padding(2);
            this.pnlButtons.Name = "pnlButtons";
            this.pnlButtons.Size = new System.Drawing.Size(734, 42);
            this.pnlButtons.TabIndex = 1;
            // 
            // btnCancel
            // 
            this.btnCancel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCancel.Location = new System.Drawing.Point(371, 7);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(2);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(65, 30);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnSave
            // 
            this.btnSave.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSave.Location = new System.Drawing.Point(300, 7);
            this.btnSave.Margin = new System.Windows.Forms.Padding(2);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(65, 30);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // CreateFundForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.ClientSize = new System.Drawing.Size(734, 150);
            this.Controls.Add(this.pnlButtons);
            this.Controls.Add(this.pnlCreateFund);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CreateFundForm";
            this.Text = "Create Fund";
            this.Load += new System.EventHandler(this.CreateFundForm_Load);
            this.pnlCreateFund.ResumeLayout(false);
            this.pnlCreateFund.PerformLayout();
            this.pnlFundId.ResumeLayout(false);
            this.pnlFundId.PerformLayout();
            this.pnlName.ResumeLayout(false);
            this.pnlName.PerformLayout();
            this.pnlDescription.ResumeLayout(false);
            this.pnlDescription.PerformLayout();
            this.pnlInitialBalance.ResumeLayout(false);
            this.pnlInitialBalance.PerformLayout();
            this.pnlButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel pnlCreateFund;
        private System.Windows.Forms.Panel pnlButtons;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Panel pnlFundId;
        private System.Windows.Forms.Panel pnlName;
        private System.Windows.Forms.Panel pnlDescription;
        private System.Windows.Forms.Panel pnlInitialBalance;
        private System.Windows.Forms.TextBox txtFundId;
        private System.Windows.Forms.Label lblFundId;
        private System.Windows.Forms.Label lblFundName;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.Label lblInitialBalance;
        private System.Windows.Forms.TextBox txtFundName;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.MaskedTextBox txtInitialBalance;
    }
}