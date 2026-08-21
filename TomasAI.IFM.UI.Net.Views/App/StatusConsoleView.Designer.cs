namespace TomasAI.IFM.UI.Net.Views.App
{
    partial class StatusConsoleView
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
            lstStatusConsole = new ListView();
            colStatusTime = new ColumnHeader();
            colStatus = new ColumnHeader();
            pnlTitle = new Panel();
            lblTitle = new Label();
            pnlTitle.SuspendLayout();
            SuspendLayout();
            //
            // lstStatusConsole
            //
            lstStatusConsole.BackColor = Color.Black;
            lstStatusConsole.BorderStyle = BorderStyle.None;
            lstStatusConsole.Columns.AddRange(new ColumnHeader[] { colStatusTime, colStatus });
            lstStatusConsole.Dock = DockStyle.Fill;
            lstStatusConsole.ForeColor = Color.White;
            lstStatusConsole.FullRowSelect = true;
            lstStatusConsole.Location = new Point(0, 25);
            lstStatusConsole.Margin = new Padding(4, 3, 4, 3);
            lstStatusConsole.Name = "lstStatusConsole";
            lstStatusConsole.Size = new Size(550, 283);
            lstStatusConsole.TabIndex = 1;
            lstStatusConsole.UseCompatibleStateImageBehavior = false;
            lstStatusConsole.View = View.Details;
            //
            // colStatusTime
            //
            colStatusTime.Text = "Status Time";
            colStatusTime.Width = 170;
            //
            // colStatus
            //
            colStatus.Text = "Status";
            colStatus.Width = 500;
            //
            // pnlTitle
            //
            pnlTitle.BackColor = Color.Black;
            pnlTitle.Controls.Add(lblTitle);
            pnlTitle.Dock = DockStyle.Top;
            pnlTitle.Location = new Point(0, 0);
            pnlTitle.Margin = new Padding(4, 3, 4, 3);
            pnlTitle.Name = "pnlTitle";
            pnlTitle.Size = new Size(550, 25);
            pnlTitle.TabIndex = 0;
            //
            // lblTitle
            //
            lblTitle.AutoSize = true;
            lblTitle.BackColor = Color.Black;
            lblTitle.Dock = DockStyle.Left;
            lblTitle.Font = new Font("Microsoft Sans Serif", 11.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(0, 0);
            lblTitle.Margin = new Padding(4, 0, 4, 0);
            lblTitle.Name = "lblTitle";
            lblTitle.Padding = new Padding(0, 3, 0, 0);
            lblTitle.Size = new Size(127, 21);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Status Console";
            //
            // StatusConsoleView
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Black;
            Controls.Add(lstStatusConsole);
            Controls.Add(pnlTitle);
            Margin = new Padding(4, 3, 4, 3);
            Name = "StatusConsoleView";
            Size = new Size(550, 308);
            pnlTitle.ResumeLayout(false);
            pnlTitle.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.ListView lstStatusConsole;
        private System.Windows.Forms.ColumnHeader colStatusTime;
        private System.Windows.Forms.ColumnHeader colStatus;
        private System.Windows.Forms.Panel pnlTitle;
        private System.Windows.Forms.Label lblTitle;
    }
}
