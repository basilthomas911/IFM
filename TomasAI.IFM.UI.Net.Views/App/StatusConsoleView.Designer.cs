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
            // StatusConsoleView
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Black;
            Controls.Add(lstStatusConsole);
            Margin = new Padding(4, 3, 4, 3);
            Name = "StatusConsoleView";
            Size = new Size(550, 308);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.ListView lstStatusConsole;
        private System.Windows.Forms.ColumnHeader colStatusTime;
        private System.Windows.Forms.ColumnHeader colStatus;
    }
}
