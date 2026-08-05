
namespace TomasAI.IFM.UI.Net.Views.SystemInfo
{
    partial class SystemWaitView
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
            this.lblWaitInfo = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lblWaitInfo
            // 
            this.lblWaitInfo.AutoSize = true;
            this.lblWaitInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblWaitInfo.Location = new System.Drawing.Point(58, 9);
            this.lblWaitInfo.Name = "lblWaitInfo";
            this.lblWaitInfo.Size = new System.Drawing.Size(0, 24);
            this.lblWaitInfo.TabIndex = 0;
            this.lblWaitInfo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SystemWaitView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(803, 42);
            this.Controls.Add(this.lblWaitInfo);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SystemWaitView";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Wait Action";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.SystemWaitView_FormClosed);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblWaitInfo;
    }
}