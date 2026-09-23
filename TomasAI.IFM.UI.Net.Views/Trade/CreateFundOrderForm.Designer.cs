namespace TomasAI.IFM.UI.Net.Views.Trade
{
    partial class CreateFundOrderForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            lblOrderId = new System.Windows.Forms.Label();
            txtOrderId = new System.Windows.Forms.TextBox();
            lblOrderDate = new System.Windows.Forms.Label();
            txtOrderDate = new System.Windows.Forms.TextBox();
            lblOrderStatus = new System.Windows.Forms.Label();
            txtOrderStatus = new System.Windows.Forms.TextBox();
            lblReference = new System.Windows.Forms.Label();
            txtReference = new System.Windows.Forms.TextBox();
            pnlButtons = new System.Windows.Forms.Panel();
            btnCancel = new System.Windows.Forms.Button();
            btnSave = new System.Windows.Forms.Button();
            tableLayoutPanel1.SuspendLayout();
            pnlButtons.SuspendLayout();
            SuspendLayout();
            //
            // tableLayoutPanel1
            //
            tableLayoutPanel1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(lblOrderId, 0, 0);
            tableLayoutPanel1.Controls.Add(txtOrderId, 1, 0);
            tableLayoutPanel1.Controls.Add(lblOrderDate, 0, 1);
            tableLayoutPanel1.Controls.Add(txtOrderDate, 1, 1);
            tableLayoutPanel1.Controls.Add(lblOrderStatus, 0, 2);
            tableLayoutPanel1.Controls.Add(txtOrderStatus, 1, 2);
            tableLayoutPanel1.Controls.Add(lblReference, 0, 3);
            tableLayoutPanel1.Controls.Add(txtReference, 1, 3);
            tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(12);
            tableLayoutPanel1.RowCount = 4;
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new System.Drawing.Size(721, 294);
            tableLayoutPanel1.TabIndex = 0;
            //
            // labels
            //
            lblOrderId.AutoSize = true;
            lblOrderId.Dock = System.Windows.Forms.DockStyle.Fill;
            lblOrderId.ForeColor = System.Drawing.Color.White;
            lblOrderId.Text = "Order Id:";
            lblOrderId.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            lblOrderDate.AutoSize = true;
            lblOrderDate.Dock = System.Windows.Forms.DockStyle.Fill;
            lblOrderDate.ForeColor = System.Drawing.Color.White;
            lblOrderDate.Text = "Order Date:";
            lblOrderDate.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            lblOrderStatus.AutoSize = true;
            lblOrderStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            lblOrderStatus.ForeColor = System.Drawing.Color.White;
            lblOrderStatus.Text = "Order Status:";
            lblOrderStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            lblReference.AutoSize = true;
            lblReference.Dock = System.Windows.Forms.DockStyle.Fill;
            lblReference.ForeColor = System.Drawing.Color.White;
            lblReference.Text = "Reference:";
            lblReference.TextAlign = System.Drawing.ContentAlignment.TopRight;
            //
            // read-only fields
            //
            txtOrderId.Dock = System.Windows.Forms.DockStyle.Fill;
            txtOrderId.ReadOnly = true;
            txtOrderId.TabStop = false;
            txtOrderDate.Dock = System.Windows.Forms.DockStyle.Fill;
            txtOrderDate.ReadOnly = true;
            txtOrderDate.TabStop = false;
            txtOrderStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            txtOrderStatus.ReadOnly = true;
            txtOrderStatus.TabStop = false;
            //
            // txtReference
            //
            txtReference.AcceptsReturn = true;
            txtReference.AccessibleName = "Optional multiline order reference";
            txtReference.Dock = System.Windows.Forms.DockStyle.Fill;
            txtReference.Multiline = true;
            txtReference.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtReference.TabIndex = 0;
            txtReference.TextChanged += txtReference_TextChanged;
            //
            // pnlButtons
            //
            pnlButtons.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
            pnlButtons.Controls.Add(btnCancel);
            pnlButtons.Controls.Add(btnSave);
            pnlButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            pnlButtons.Size = new System.Drawing.Size(721, 56);
            pnlButtons.TabIndex = 1;
            //
            // btnCancel
            //
            btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCancel.Location = new System.Drawing.Point(632, 13);
            btnCancel.Size = new System.Drawing.Size(77, 30);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "&Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            //
            // btnSave
            //
            btnSave.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnSave.Location = new System.Drawing.Point(547, 13);
            btnSave.Size = new System.Drawing.Size(77, 30);
            btnSave.TabIndex = 1;
            btnSave.Text = "&Save";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            //
            // CreateFundOrderForm
            //
            AcceptButton = btnSave;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new System.Drawing.Size(721, 350);
            Controls.Add(tableLayoutPanel1);
            Controls.Add(pnlButtons);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "CreateFundOrderForm";
            SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Add Fund Order";
            FormClosed += CreateFundOrderForm_FormClosed;
            Load += CreateFundOrderForm_Load;
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            pnlButtons.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label lblOrderId;
        private System.Windows.Forms.TextBox txtOrderId;
        private System.Windows.Forms.Label lblOrderDate;
        private System.Windows.Forms.TextBox txtOrderDate;
        private System.Windows.Forms.Label lblOrderStatus;
        private System.Windows.Forms.TextBox txtOrderStatus;
        private System.Windows.Forms.Label lblReference;
        private System.Windows.Forms.TextBox txtReference;
        private System.Windows.Forms.Panel pnlButtons;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
    }
}