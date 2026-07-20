namespace TomasAI.IFM.Views.Trade
{
    partial class TradeOrderConfirmationForm
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
            this.txtName = new System.Windows.Forms.TextBox();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.txtAction = new System.Windows.Forms.TextBox();
            this.pnlTradeOrderConfirmation = new System.Windows.Forms.TableLayoutPanel();
            this.pnlOrderType = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.txtOrderType = new System.Windows.Forms.TextBox();
            this.pnlOrderPrice = new System.Windows.Forms.Panel();
            this.lblOrderPrice = new System.Windows.Forms.Label();
            this.txtOrderPrice = new System.Windows.Forms.TextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.lblOrderAmount = new System.Windows.Forms.Label();
            this.txtOrderAmount = new System.Windows.Forms.TextBox();
            this.pnlCommission = new System.Windows.Forms.Panel();
            this.lblCommission = new System.Windows.Forms.Label();
            this.txtCommission = new System.Windows.Forms.TextBox();
            this.pnlTotalAmount = new System.Windows.Forms.Panel();
            this.lblTotalAmount = new System.Windows.Forms.Label();
            this.txtTotalAmount = new System.Windows.Forms.TextBox();
            this.pnlTradeFillType = new System.Windows.Forms.Panel();
            this.lblTradeFillType = new System.Windows.Forms.Label();
            this.ddlTradeFillType = new System.Windows.Forms.ComboBox();
            this.btnContinue = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.pnlTradeOrderConfirmation.SuspendLayout();
            this.pnlOrderType.SuspendLayout();
            this.pnlOrderPrice.SuspendLayout();
            this.panel1.SuspendLayout();
            this.pnlCommission.SuspendLayout();
            this.pnlTotalAmount.SuspendLayout();
            this.pnlTradeFillType.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtName
            // 
            this.txtName.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.txtName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtName.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtName.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtName.ForeColor = System.Drawing.Color.White;
            this.txtName.Location = new System.Drawing.Point(0, 0);
            this.txtName.Margin = new System.Windows.Forms.Padding(2);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(338, 26);
            this.txtName.TabIndex = 0;
            this.txtName.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtDescription
            // 
            this.txtDescription.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.txtDescription.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDescription.ForeColor = System.Drawing.Color.White;
            this.txtDescription.Location = new System.Drawing.Point(0, 26);
            this.txtDescription.Margin = new System.Windows.Forms.Padding(2);
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.Size = new System.Drawing.Size(338, 23);
            this.txtDescription.TabIndex = 1;
            this.txtDescription.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtAction
            // 
            this.txtAction.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.txtAction.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtAction.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtAction.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtAction.ForeColor = System.Drawing.Color.Aqua;
            this.txtAction.Location = new System.Drawing.Point(0, 49);
            this.txtAction.Margin = new System.Windows.Forms.Padding(2);
            this.txtAction.Name = "txtAction";
            this.txtAction.Size = new System.Drawing.Size(338, 22);
            this.txtAction.TabIndex = 2;
            this.txtAction.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // pnlTradeOrderConfirmation
            // 
            this.pnlTradeOrderConfirmation.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
            this.pnlTradeOrderConfirmation.ColumnCount = 2;
            this.pnlTradeOrderConfirmation.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30.88889F));
            this.pnlTradeOrderConfirmation.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 69.11111F));
            this.pnlTradeOrderConfirmation.Controls.Add(this.pnlOrderType, 0, 0);
            this.pnlTradeOrderConfirmation.Controls.Add(this.txtOrderType, 1, 0);
            this.pnlTradeOrderConfirmation.Controls.Add(this.pnlOrderPrice, 0, 1);
            this.pnlTradeOrderConfirmation.Controls.Add(this.txtOrderPrice, 1, 1);
            this.pnlTradeOrderConfirmation.Controls.Add(this.panel1, 0, 2);
            this.pnlTradeOrderConfirmation.Controls.Add(this.txtOrderAmount, 1, 2);
            this.pnlTradeOrderConfirmation.Controls.Add(this.pnlCommission, 0, 3);
            this.pnlTradeOrderConfirmation.Controls.Add(this.txtCommission, 1, 3);
            this.pnlTradeOrderConfirmation.Controls.Add(this.pnlTotalAmount, 0, 4);
            this.pnlTradeOrderConfirmation.Controls.Add(this.txtTotalAmount, 1, 4);
            this.pnlTradeOrderConfirmation.Controls.Add(this.pnlTradeFillType, 0, 5);
            this.pnlTradeOrderConfirmation.Controls.Add(this.ddlTradeFillType, 1, 5);
            this.pnlTradeOrderConfirmation.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTradeOrderConfirmation.Location = new System.Drawing.Point(0, 71);
            this.pnlTradeOrderConfirmation.Margin = new System.Windows.Forms.Padding(2);
            this.pnlTradeOrderConfirmation.Name = "pnlTradeOrderConfirmation";
            this.pnlTradeOrderConfirmation.RowCount = 6;
            this.pnlTradeOrderConfirmation.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.pnlTradeOrderConfirmation.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.pnlTradeOrderConfirmation.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.pnlTradeOrderConfirmation.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.pnlTradeOrderConfirmation.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.pnlTradeOrderConfirmation.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 13F));
            this.pnlTradeOrderConfirmation.Size = new System.Drawing.Size(338, 167);
            this.pnlTradeOrderConfirmation.TabIndex = 3;
            // 
            // pnlOrderType
            // 
            this.pnlOrderType.Controls.Add(this.label1);
            this.pnlOrderType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlOrderType.Location = new System.Drawing.Point(3, 3);
            this.pnlOrderType.Margin = new System.Windows.Forms.Padding(2);
            this.pnlOrderType.Name = "pnlOrderType";
            this.pnlOrderType.Size = new System.Drawing.Size(99, 22);
            this.pnlOrderType.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(19, 2);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(85, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "Order Type:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtOrderType
            // 
            this.txtOrderType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtOrderType.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtOrderType.Location = new System.Drawing.Point(107, 3);
            this.txtOrderType.Margin = new System.Windows.Forms.Padding(2);
            this.txtOrderType.Name = "txtOrderType";
            this.txtOrderType.ReadOnly = true;
            this.txtOrderType.Size = new System.Drawing.Size(228, 23);
            this.txtOrderType.TabIndex = 1;
            // 
            // pnlOrderPrice
            // 
            this.pnlOrderPrice.Controls.Add(this.lblOrderPrice);
            this.pnlOrderPrice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlOrderPrice.Location = new System.Drawing.Point(3, 30);
            this.pnlOrderPrice.Margin = new System.Windows.Forms.Padding(2);
            this.pnlOrderPrice.Name = "pnlOrderPrice";
            this.pnlOrderPrice.Size = new System.Drawing.Size(99, 22);
            this.pnlOrderPrice.TabIndex = 2;
            // 
            // lblOrderPrice
            // 
            this.lblOrderPrice.AutoSize = true;
            this.lblOrderPrice.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblOrderPrice.ForeColor = System.Drawing.Color.White;
            this.lblOrderPrice.Location = new System.Drawing.Point(19, 2);
            this.lblOrderPrice.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblOrderPrice.Name = "lblOrderPrice";
            this.lblOrderPrice.Size = new System.Drawing.Size(85, 17);
            this.lblOrderPrice.TabIndex = 0;
            this.lblOrderPrice.Text = "Order Price:";
            this.lblOrderPrice.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtOrderPrice
            // 
            this.txtOrderPrice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtOrderPrice.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtOrderPrice.Location = new System.Drawing.Point(107, 30);
            this.txtOrderPrice.Margin = new System.Windows.Forms.Padding(2);
            this.txtOrderPrice.Name = "txtOrderPrice";
            this.txtOrderPrice.ReadOnly = true;
            this.txtOrderPrice.Size = new System.Drawing.Size(228, 23);
            this.txtOrderPrice.TabIndex = 3;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.lblOrderAmount);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(3, 57);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(99, 22);
            this.panel1.TabIndex = 4;
            // 
            // lblOrderAmount
            // 
            this.lblOrderAmount.AutoSize = true;
            this.lblOrderAmount.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblOrderAmount.ForeColor = System.Drawing.Color.White;
            this.lblOrderAmount.Location = new System.Drawing.Point(3, 2);
            this.lblOrderAmount.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblOrderAmount.Name = "lblOrderAmount";
            this.lblOrderAmount.Size = new System.Drawing.Size(101, 17);
            this.lblOrderAmount.TabIndex = 0;
            this.lblOrderAmount.Text = "Order Amount:";
            this.lblOrderAmount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtOrderAmount
            // 
            this.txtOrderAmount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtOrderAmount.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtOrderAmount.Location = new System.Drawing.Point(107, 57);
            this.txtOrderAmount.Margin = new System.Windows.Forms.Padding(2);
            this.txtOrderAmount.Name = "txtOrderAmount";
            this.txtOrderAmount.ReadOnly = true;
            this.txtOrderAmount.Size = new System.Drawing.Size(228, 23);
            this.txtOrderAmount.TabIndex = 5;
            // 
            // pnlCommission
            // 
            this.pnlCommission.Controls.Add(this.lblCommission);
            this.pnlCommission.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlCommission.Location = new System.Drawing.Point(3, 84);
            this.pnlCommission.Margin = new System.Windows.Forms.Padding(2);
            this.pnlCommission.Name = "pnlCommission";
            this.pnlCommission.Size = new System.Drawing.Size(99, 22);
            this.pnlCommission.TabIndex = 6;
            // 
            // lblCommission
            // 
            this.lblCommission.AutoSize = true;
            this.lblCommission.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCommission.ForeColor = System.Drawing.Color.White;
            this.lblCommission.Location = new System.Drawing.Point(16, 2);
            this.lblCommission.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblCommission.Name = "lblCommission";
            this.lblCommission.Size = new System.Drawing.Size(87, 17);
            this.lblCommission.TabIndex = 0;
            this.lblCommission.Text = "Commission:";
            this.lblCommission.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtCommission
            // 
            this.txtCommission.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCommission.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtCommission.Location = new System.Drawing.Point(107, 84);
            this.txtCommission.Margin = new System.Windows.Forms.Padding(2);
            this.txtCommission.Name = "txtCommission";
            this.txtCommission.ReadOnly = true;
            this.txtCommission.Size = new System.Drawing.Size(228, 23);
            this.txtCommission.TabIndex = 7;
            // 
            // pnlTotalAmount
            // 
            this.pnlTotalAmount.Controls.Add(this.lblTotalAmount);
            this.pnlTotalAmount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlTotalAmount.Location = new System.Drawing.Point(3, 111);
            this.pnlTotalAmount.Margin = new System.Windows.Forms.Padding(2);
            this.pnlTotalAmount.Name = "pnlTotalAmount";
            this.pnlTotalAmount.Size = new System.Drawing.Size(99, 22);
            this.pnlTotalAmount.TabIndex = 8;
            // 
            // lblTotalAmount
            // 
            this.lblTotalAmount.AutoSize = true;
            this.lblTotalAmount.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTotalAmount.ForeColor = System.Drawing.Color.White;
            this.lblTotalAmount.Location = new System.Drawing.Point(8, 2);
            this.lblTotalAmount.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblTotalAmount.Name = "lblTotalAmount";
            this.lblTotalAmount.Size = new System.Drawing.Size(96, 17);
            this.lblTotalAmount.TabIndex = 0;
            this.lblTotalAmount.Text = "Total Amount:";
            this.lblTotalAmount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtTotalAmount
            // 
            this.txtTotalAmount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTotalAmount.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtTotalAmount.Location = new System.Drawing.Point(107, 111);
            this.txtTotalAmount.Margin = new System.Windows.Forms.Padding(2);
            this.txtTotalAmount.Name = "txtTotalAmount";
            this.txtTotalAmount.ReadOnly = true;
            this.txtTotalAmount.Size = new System.Drawing.Size(228, 23);
            this.txtTotalAmount.TabIndex = 9;
            // 
            // pnlTradeFillType
            // 
            this.pnlTradeFillType.Controls.Add(this.lblTradeFillType);
            this.pnlTradeFillType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlTradeFillType.Location = new System.Drawing.Point(3, 138);
            this.pnlTradeFillType.Margin = new System.Windows.Forms.Padding(2);
            this.pnlTradeFillType.Name = "pnlTradeFillType";
            this.pnlTradeFillType.Size = new System.Drawing.Size(99, 26);
            this.pnlTradeFillType.TabIndex = 10;
            // 
            // lblTradeFillType
            // 
            this.lblTradeFillType.AutoSize = true;
            this.lblTradeFillType.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTradeFillType.ForeColor = System.Drawing.Color.White;
            this.lblTradeFillType.Location = new System.Drawing.Point(-3, 2);
            this.lblTradeFillType.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblTradeFillType.Name = "lblTradeFillType";
            this.lblTradeFillType.Size = new System.Drawing.Size(107, 17);
            this.lblTradeFillType.TabIndex = 0;
            this.lblTradeFillType.Text = "Trade Fill Type:";
            this.lblTradeFillType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // ddlTradeFillType
            // 
            this.ddlTradeFillType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ddlTradeFillType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlTradeFillType.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlTradeFillType.FormattingEnabled = true;
            this.ddlTradeFillType.Location = new System.Drawing.Point(107, 138);
            this.ddlTradeFillType.Margin = new System.Windows.Forms.Padding(2);
            this.ddlTradeFillType.Name = "ddlTradeFillType";
            this.ddlTradeFillType.Size = new System.Drawing.Size(228, 24);
            this.ddlTradeFillType.TabIndex = 11;
            this.ddlTradeFillType.SelectedIndexChanged += new System.EventHandler(this.ddlTradeFillType_SelectedIndexChanged);
            // 
            // btnContinue
            // 
            this.btnContinue.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnContinue.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnContinue.Location = new System.Drawing.Point(87, 243);
            this.btnContinue.Margin = new System.Windows.Forms.Padding(2);
            this.btnContinue.Name = "btnContinue";
            this.btnContinue.Size = new System.Drawing.Size(80, 28);
            this.btnContinue.TabIndex = 4;
            this.btnContinue.Text = "Submit";
            this.btnContinue.UseVisualStyleBackColor = true;
            this.btnContinue.Click += new System.EventHandler(this.btnContinue_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCancel.Location = new System.Drawing.Point(171, 243);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(2);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(80, 28);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // TradeOrderConfirmationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(338, 278);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnContinue);
            this.Controls.Add(this.pnlTradeOrderConfirmation);
            this.Controls.Add(this.txtAction);
            this.Controls.Add(this.txtDescription);
            this.Controls.Add(this.txtName);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TradeOrderConfirmationForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Trade Order Confirmation";
            this.Load += new System.EventHandler(this.TradeOrderConfirmationForm_Load);
            this.pnlTradeOrderConfirmation.ResumeLayout(false);
            this.pnlTradeOrderConfirmation.PerformLayout();
            this.pnlOrderType.ResumeLayout(false);
            this.pnlOrderType.PerformLayout();
            this.pnlOrderPrice.ResumeLayout(false);
            this.pnlOrderPrice.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.pnlCommission.ResumeLayout(false);
            this.pnlCommission.PerformLayout();
            this.pnlTotalAmount.ResumeLayout(false);
            this.pnlTotalAmount.PerformLayout();
            this.pnlTradeFillType.ResumeLayout(false);
            this.pnlTradeFillType.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.TextBox txtAction;
        private System.Windows.Forms.TableLayoutPanel pnlTradeOrderConfirmation;
        private System.Windows.Forms.Panel pnlOrderType;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtOrderType;
        private System.Windows.Forms.Button btnContinue;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Panel pnlOrderPrice;
        private System.Windows.Forms.Label lblOrderPrice;
        private System.Windows.Forms.TextBox txtOrderPrice;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblOrderAmount;
        private System.Windows.Forms.TextBox txtOrderAmount;
        private System.Windows.Forms.Panel pnlCommission;
        private System.Windows.Forms.Label lblCommission;
        private System.Windows.Forms.TextBox txtCommission;
        private System.Windows.Forms.Panel pnlTotalAmount;
        private System.Windows.Forms.Label lblTotalAmount;
        private System.Windows.Forms.TextBox txtTotalAmount;
        private System.Windows.Forms.Panel pnlTradeFillType;
        private System.Windows.Forms.Label lblTradeFillType;
        private System.Windows.Forms.ComboBox ddlTradeFillType;
    }
}