namespace TomasAI.IFM.UI.Net.Views.Trade
{
    partial class SetLocalSymbolForm
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
            this.pnlLocalSymbol = new System.Windows.Forms.Panel();
            this.txtLocalSymbol = new System.Windows.Forms.TextBox();
            this.lblLocalSymbol = new System.Windows.Forms.Label();
            this.pnlButtons = new System.Windows.Forms.Panel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.pnlLocalSymbol.SuspendLayout();
            this.pnlButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlLocalSymbol
            // 
            this.pnlLocalSymbol.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlLocalSymbol.Controls.Add(this.txtLocalSymbol);
            this.pnlLocalSymbol.Controls.Add(this.lblLocalSymbol);
            this.pnlLocalSymbol.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlLocalSymbol.Location = new System.Drawing.Point(0, 0);
            this.pnlLocalSymbol.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.pnlLocalSymbol.Name = "pnlLocalSymbol";
            this.pnlLocalSymbol.Size = new System.Drawing.Size(275, 39);
            this.pnlLocalSymbol.TabIndex = 0;
            // 
            // txtLocalSymbol
            // 
            this.txtLocalSymbol.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtLocalSymbol.Location = new System.Drawing.Point(106, 11);
            this.txtLocalSymbol.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtLocalSymbol.Name = "txtLocalSymbol";
            this.txtLocalSymbol.Size = new System.Drawing.Size(114, 23);
            this.txtLocalSymbol.TabIndex = 1;
            // 
            // lblLocalSymbol
            // 
            this.lblLocalSymbol.AutoSize = true;
            this.lblLocalSymbol.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLocalSymbol.ForeColor = System.Drawing.Color.White;
            this.lblLocalSymbol.Location = new System.Drawing.Point(9, 13);
            this.lblLocalSymbol.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblLocalSymbol.Name = "lblLocalSymbol";
            this.lblLocalSymbol.Size = new System.Drawing.Size(96, 17);
            this.lblLocalSymbol.TabIndex = 0;
            this.lblLocalSymbol.Text = "Local Symbol:";
            this.lblLocalSymbol.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // pnlButtons
            // 
            this.pnlButtons.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlButtons.Controls.Add(this.btnCancel);
            this.pnlButtons.Controls.Add(this.btnSave);
            this.pnlButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlButtons.Location = new System.Drawing.Point(0, 44);
            this.pnlButtons.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.pnlButtons.Name = "pnlButtons";
            this.pnlButtons.Size = new System.Drawing.Size(275, 37);
            this.pnlButtons.TabIndex = 1;
            // 
            // btnCancel
            // 
            this.btnCancel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCancel.Location = new System.Drawing.Point(142, 4);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(56, 24);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnSave
            // 
            this.btnSave.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSave.Location = new System.Drawing.Point(81, 4);
            this.btnSave.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(56, 24);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "&Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // SetLocalSymbolForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.ClientSize = new System.Drawing.Size(275, 81);
            this.Controls.Add(this.pnlButtons);
            this.Controls.Add(this.pnlLocalSymbol);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SetLocalSymbolForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Set Local Symbol";
            this.pnlLocalSymbol.ResumeLayout(false);
            this.pnlLocalSymbol.PerformLayout();
            this.pnlButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlLocalSymbol;
        private System.Windows.Forms.TextBox txtLocalSymbol;
        private System.Windows.Forms.Label lblLocalSymbol;
        private System.Windows.Forms.Panel pnlButtons;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
    }
}