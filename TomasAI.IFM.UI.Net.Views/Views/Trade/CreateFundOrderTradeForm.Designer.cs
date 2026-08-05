namespace TomasAI.IFM.UI.Net.Views.Trade
{
    partial class CreateFundOrderTradeForm
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
            tableLayoutPanel1 = new TableLayoutPanel();
            pnlOrderId = new Panel();
            lblTradeId = new Label();
            pnlOrderDate = new Panel();
            lblTradeType = new Label();
            txtTradeId = new TextBox();
            pnlOrderStatus = new Panel();
            lblTradeDate = new Label();
            panel1 = new Panel();
            lblMaturityDate = new Label();
            ddlTradeType = new ComboBox();
            dtpTradeDate = new DateTimePicker();
            dtpMaturityDate = new DateTimePicker();
            pnlTradeState = new Panel();
            lblTradeState = new Label();
            txtTradeState = new TextBox();
            pnlTradeAction = new Panel();
            lblTradeAction = new Label();
            txtTradeAction = new TextBox();
            panel2 = new Panel();
            lblReference = new Label();
            txtReference = new TextBox();
            pnlBaseContractSymbol = new Panel();
            lblBaseContractSymbol = new Label();
            ddlBaseSymbol = new ComboBox();
            pnlButtons = new Panel();
            btnCancel = new Button();
            btnSave = new Button();
            tableLayoutPanel1.SuspendLayout();
            pnlOrderId.SuspendLayout();
            pnlOrderDate.SuspendLayout();
            pnlOrderStatus.SuspendLayout();
            panel1.SuspendLayout();
            pnlTradeState.SuspendLayout();
            pnlTradeAction.SuspendLayout();
            panel2.SuspendLayout();
            pnlBaseContractSymbol.SuspendLayout();
            pnlButtons.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.BackColor = Color.FromArgb(64, 64, 64);
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F));
            tableLayoutPanel1.Controls.Add(pnlOrderId, 0, 0);
            tableLayoutPanel1.Controls.Add(pnlOrderDate, 0, 1);
            tableLayoutPanel1.Controls.Add(txtTradeId, 1, 0);
            tableLayoutPanel1.Controls.Add(pnlOrderStatus, 0, 2);
            tableLayoutPanel1.Controls.Add(panel1, 0, 3);
            tableLayoutPanel1.Controls.Add(ddlTradeType, 1, 1);
            tableLayoutPanel1.Controls.Add(dtpTradeDate, 1, 2);
            tableLayoutPanel1.Controls.Add(dtpMaturityDate, 1, 3);
            tableLayoutPanel1.Controls.Add(pnlTradeState, 0, 4);
            tableLayoutPanel1.Controls.Add(txtTradeState, 1, 4);
            tableLayoutPanel1.Controls.Add(pnlTradeAction, 0, 5);
            tableLayoutPanel1.Controls.Add(txtTradeAction, 1, 5);
            tableLayoutPanel1.Controls.Add(panel2, 0, 6);
            tableLayoutPanel1.Controls.Add(txtReference, 1, 6);
            tableLayoutPanel1.Controls.Add(pnlBaseContractSymbol, 0, 7);
            tableLayoutPanel1.Controls.Add(ddlBaseSymbol, 1, 7);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Margin = new Padding(2);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 9;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 23F));
            tableLayoutPanel1.Size = new Size(621, 312);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // pnlOrderId
            // 
            pnlOrderId.Controls.Add(lblTradeId);
            pnlOrderId.Dock = DockStyle.Fill;
            pnlOrderId.Location = new Point(2, 2);
            pnlOrderId.Margin = new Padding(2);
            pnlOrderId.Name = "pnlOrderId";
            pnlOrderId.Size = new Size(151, 26);
            pnlOrderId.TabIndex = 0;
            // 
            // lblTradeId
            // 
            lblTradeId.AutoSize = true;
            lblTradeId.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTradeId.ForeColor = SystemColors.ControlLightLight;
            lblTradeId.Location = new Point(72, 2);
            lblTradeId.Margin = new Padding(2, 0, 2, 0);
            lblTradeId.Name = "lblTradeId";
            lblTradeId.Size = new Size(65, 17);
            lblTradeId.TabIndex = 0;
            lblTradeId.Text = "Trade Id:";
            // 
            // pnlOrderDate
            // 
            pnlOrderDate.Controls.Add(lblTradeType);
            pnlOrderDate.Dock = DockStyle.Fill;
            pnlOrderDate.Location = new Point(2, 32);
            pnlOrderDate.Margin = new Padding(2);
            pnlOrderDate.Name = "pnlOrderDate";
            pnlOrderDate.Size = new Size(151, 26);
            pnlOrderDate.TabIndex = 1;
            // 
            // lblTradeType
            // 
            lblTradeType.AutoSize = true;
            lblTradeType.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTradeType.ForeColor = SystemColors.ControlLightLight;
            lblTradeType.Location = new Point(52, 3);
            lblTradeType.Margin = new Padding(2, 0, 2, 0);
            lblTradeType.Name = "lblTradeType";
            lblTradeType.Size = new Size(86, 17);
            lblTradeType.TabIndex = 0;
            lblTradeType.Text = "Trade Type:";
            // 
            // txtTradeId
            // 
            txtTradeId.Dock = DockStyle.Left;
            txtTradeId.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtTradeId.Location = new Point(157, 2);
            txtTradeId.Margin = new Padding(2);
            txtTradeId.Name = "txtTradeId";
            txtTradeId.ReadOnly = true;
            txtTradeId.Size = new Size(163, 23);
            txtTradeId.TabIndex = 2;
            // 
            // pnlOrderStatus
            // 
            pnlOrderStatus.Controls.Add(lblTradeDate);
            pnlOrderStatus.Dock = DockStyle.Fill;
            pnlOrderStatus.Location = new Point(2, 62);
            pnlOrderStatus.Margin = new Padding(2);
            pnlOrderStatus.Name = "pnlOrderStatus";
            pnlOrderStatus.Size = new Size(151, 26);
            pnlOrderStatus.TabIndex = 4;
            // 
            // lblTradeDate
            // 
            lblTradeDate.AutoSize = true;
            lblTradeDate.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTradeDate.ForeColor = SystemColors.ControlLightLight;
            lblTradeDate.Location = new Point(52, 6);
            lblTradeDate.Margin = new Padding(2, 0, 2, 0);
            lblTradeDate.Name = "lblTradeDate";
            lblTradeDate.Size = new Size(84, 17);
            lblTradeDate.TabIndex = 0;
            lblTradeDate.Text = "Trade Date:";
            // 
            // panel1
            // 
            panel1.Controls.Add(lblMaturityDate);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(2, 92);
            panel1.Margin = new Padding(2);
            panel1.Name = "panel1";
            panel1.Size = new Size(151, 26);
            panel1.TabIndex = 6;
            // 
            // lblMaturityDate
            // 
            lblMaturityDate.AutoSize = true;
            lblMaturityDate.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblMaturityDate.ForeColor = SystemColors.ControlLightLight;
            lblMaturityDate.Location = new Point(37, 6);
            lblMaturityDate.Margin = new Padding(2, 0, 2, 0);
            lblMaturityDate.Name = "lblMaturityDate";
            lblMaturityDate.Size = new Size(96, 17);
            lblMaturityDate.TabIndex = 0;
            lblMaturityDate.Text = "Maturity Date:";
            // 
            // ddlTradeType
            // 
            ddlTradeType.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlTradeType.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlTradeType.FormattingEnabled = true;
            ddlTradeType.Location = new Point(157, 32);
            ddlTradeType.Margin = new Padding(2);
            ddlTradeType.Name = "ddlTradeType";
            ddlTradeType.Size = new Size(311, 25);
            ddlTradeType.TabIndex = 7;
            ddlTradeType.SelectedIndexChanged += ddlTradeType_SelectedIndexChanged;
            // 
            // dtpTradeDate
            // 
            dtpTradeDate.CustomFormat = "yyyy-MMM-dd";
            dtpTradeDate.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dtpTradeDate.Format = DateTimePickerFormat.Custom;
            dtpTradeDate.Location = new Point(157, 62);
            dtpTradeDate.Margin = new Padding(2);
            dtpTradeDate.Name = "dtpTradeDate";
            dtpTradeDate.Size = new Size(163, 23);
            dtpTradeDate.TabIndex = 8;
            // 
            // dtpMaturityDate
            // 
            dtpMaturityDate.CustomFormat = "yyyy-MMM-dd";
            dtpMaturityDate.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dtpMaturityDate.Format = DateTimePickerFormat.Custom;
            dtpMaturityDate.Location = new Point(157, 92);
            dtpMaturityDate.Margin = new Padding(2);
            dtpMaturityDate.Name = "dtpMaturityDate";
            dtpMaturityDate.Size = new Size(163, 23);
            dtpMaturityDate.TabIndex = 9;
            // 
            // pnlTradeState
            // 
            pnlTradeState.Controls.Add(lblTradeState);
            pnlTradeState.Dock = DockStyle.Fill;
            pnlTradeState.Location = new Point(2, 122);
            pnlTradeState.Margin = new Padding(2);
            pnlTradeState.Name = "pnlTradeState";
            pnlTradeState.Size = new Size(151, 26);
            pnlTradeState.TabIndex = 10;
            // 
            // lblTradeState
            // 
            lblTradeState.AutoSize = true;
            lblTradeState.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTradeState.ForeColor = SystemColors.ControlLightLight;
            lblTradeState.Location = new Point(50, 2);
            lblTradeState.Margin = new Padding(2, 0, 2, 0);
            lblTradeState.Name = "lblTradeState";
            lblTradeState.Size = new Size(87, 17);
            lblTradeState.TabIndex = 0;
            lblTradeState.Text = "Trade State:";
            // 
            // txtTradeState
            // 
            txtTradeState.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtTradeState.Location = new Point(157, 122);
            txtTradeState.Margin = new Padding(2);
            txtTradeState.Name = "txtTradeState";
            txtTradeState.ReadOnly = true;
            txtTradeState.Size = new Size(163, 23);
            txtTradeState.TabIndex = 11;
            // 
            // pnlTradeAction
            // 
            pnlTradeAction.Controls.Add(lblTradeAction);
            pnlTradeAction.Dock = DockStyle.Fill;
            pnlTradeAction.Location = new Point(2, 152);
            pnlTradeAction.Margin = new Padding(2);
            pnlTradeAction.Name = "pnlTradeAction";
            pnlTradeAction.Size = new Size(151, 26);
            pnlTradeAction.TabIndex = 12;
            // 
            // lblTradeAction
            // 
            lblTradeAction.AutoSize = true;
            lblTradeAction.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTradeAction.ForeColor = SystemColors.ControlLightLight;
            lblTradeAction.Location = new Point(43, 2);
            lblTradeAction.Margin = new Padding(2, 0, 2, 0);
            lblTradeAction.Name = "lblTradeAction";
            lblTradeAction.Size = new Size(93, 17);
            lblTradeAction.TabIndex = 0;
            lblTradeAction.Text = "Trade Action:";
            // 
            // txtTradeAction
            // 
            txtTradeAction.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtTradeAction.Location = new Point(157, 152);
            txtTradeAction.Margin = new Padding(2);
            txtTradeAction.Name = "txtTradeAction";
            txtTradeAction.ReadOnly = true;
            txtTradeAction.Size = new Size(163, 23);
            txtTradeAction.TabIndex = 13;
            // 
            // panel2
            // 
            panel2.Controls.Add(lblReference);
            panel2.Location = new Point(2, 182);
            panel2.Margin = new Padding(2);
            panel2.Name = "panel2";
            panel2.Size = new Size(149, 24);
            panel2.TabIndex = 14;
            // 
            // lblReference
            // 
            lblReference.AutoSize = true;
            lblReference.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblReference.ForeColor = SystemColors.ControlLightLight;
            lblReference.Location = new Point(58, 2);
            lblReference.Margin = new Padding(2, 0, 2, 0);
            lblReference.Name = "lblReference";
            lblReference.Size = new Size(78, 17);
            lblReference.TabIndex = 0;
            lblReference.Text = "Reference:";
            // 
            // txtReference
            // 
            txtReference.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtReference.Location = new Point(157, 182);
            txtReference.Margin = new Padding(2);
            txtReference.Name = "txtReference";
            txtReference.Size = new Size(460, 23);
            txtReference.TabIndex = 15;
            // 
            // pnlBaseContractSymbol
            // 
            pnlBaseContractSymbol.Controls.Add(lblBaseContractSymbol);
            pnlBaseContractSymbol.Dock = DockStyle.Fill;
            pnlBaseContractSymbol.Location = new Point(4, 213);
            pnlBaseContractSymbol.Margin = new Padding(4, 3, 4, 3);
            pnlBaseContractSymbol.Name = "pnlBaseContractSymbol";
            pnlBaseContractSymbol.Size = new Size(147, 24);
            pnlBaseContractSymbol.TabIndex = 16;
            // 
            // lblBaseContractSymbol
            // 
            lblBaseContractSymbol.AutoSize = true;
            lblBaseContractSymbol.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblBaseContractSymbol.ForeColor = SystemColors.ControlLightLight;
            lblBaseContractSymbol.Location = new Point(40, 0);
            lblBaseContractSymbol.Margin = new Padding(4, 0, 4, 0);
            lblBaseContractSymbol.Name = "lblBaseContractSymbol";
            lblBaseContractSymbol.Size = new Size(94, 17);
            lblBaseContractSymbol.TabIndex = 3;
            lblBaseContractSymbol.Text = "Base Symbol:";
            // 
            // ddlBaseSymbol
            // 
            ddlBaseSymbol.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlBaseSymbol.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlBaseSymbol.FormattingEnabled = true;
            ddlBaseSymbol.Location = new Point(159, 213);
            ddlBaseSymbol.Margin = new Padding(4, 3, 4, 3);
            ddlBaseSymbol.Name = "ddlBaseSymbol";
            ddlBaseSymbol.Size = new Size(310, 25);
            ddlBaseSymbol.TabIndex = 17;
            // 
            // pnlButtons
            // 
            pnlButtons.BackColor = Color.FromArgb(64, 64, 64);
            pnlButtons.Controls.Add(btnCancel);
            pnlButtons.Controls.Add(btnSave);
            pnlButtons.Dock = DockStyle.Bottom;
            pnlButtons.Location = new Point(0, 250);
            pnlButtons.Margin = new Padding(2);
            pnlButtons.Name = "pnlButtons";
            pnlButtons.Size = new Size(621, 62);
            pnlButtons.TabIndex = 1;
            // 
            // btnCancel
            // 
            btnCancel.AutoSize = true;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnCancel.Location = new Point(318, 16);
            btnCancel.Margin = new Padding(2);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(71, 31);
            btnCancel.TabIndex = 1;
            btnCancel.Text = "&Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // btnSave
            // 
            btnSave.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnSave.Location = new Point(239, 16);
            btnSave.Margin = new Padding(2);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(75, 31);
            btnSave.TabIndex = 0;
            btnSave.Text = "&Save";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // CreateFundOrderTradeForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new Size(621, 312);
            Controls.Add(pnlButtons);
            Controls.Add(tableLayoutPanel1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(2);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "CreateFundOrderTradeForm";
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Add Trade";
            FormClosed += CreateFundOrderTradeForm_FormClosed;
            Load += CreateFundOrderTradeForm_Load;
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            pnlOrderId.ResumeLayout(false);
            pnlOrderId.PerformLayout();
            pnlOrderDate.ResumeLayout(false);
            pnlOrderDate.PerformLayout();
            pnlOrderStatus.ResumeLayout(false);
            pnlOrderStatus.PerformLayout();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            pnlTradeState.ResumeLayout(false);
            pnlTradeState.PerformLayout();
            pnlTradeAction.ResumeLayout(false);
            pnlTradeAction.PerformLayout();
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            pnlBaseContractSymbol.ResumeLayout(false);
            pnlBaseContractSymbol.PerformLayout();
            pnlButtons.ResumeLayout(false);
            pnlButtons.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel pnlOrderId;
        private System.Windows.Forms.Label lblTradeId;
        private System.Windows.Forms.Panel pnlOrderDate;
        private System.Windows.Forms.Label lblTradeType;
        private System.Windows.Forms.TextBox txtTradeId;
        private System.Windows.Forms.Panel pnlOrderStatus;
        private System.Windows.Forms.Label lblTradeDate;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblMaturityDate;
        private System.Windows.Forms.Panel pnlButtons;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.ComboBox ddlTradeType;
        private System.Windows.Forms.DateTimePicker dtpTradeDate;
        private System.Windows.Forms.DateTimePicker dtpMaturityDate;
        private System.Windows.Forms.Panel pnlTradeState;
        private System.Windows.Forms.Label lblTradeState;
        private System.Windows.Forms.TextBox txtTradeState;
        private System.Windows.Forms.Panel pnlTradeAction;
        private System.Windows.Forms.Label lblTradeAction;
        private System.Windows.Forms.TextBox txtTradeAction;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label lblReference;
        private System.Windows.Forms.TextBox txtReference;
        private System.Windows.Forms.Panel pnlBaseContractSymbol;
        private System.Windows.Forms.Label lblBaseContractSymbol;
        private System.Windows.Forms.ComboBox ddlBaseSymbol;
    }
}