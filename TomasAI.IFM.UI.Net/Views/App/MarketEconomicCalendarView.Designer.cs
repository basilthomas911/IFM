namespace TomasAI.IFM.Views.App
{
    partial class MarketEconomicCalendarView
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
            pnlTitle = new Panel();
            lblCountryCodes = new Label();
            ddlCountryCodes = new ComboBox();
            lblTitle = new Label();
            tabCalendarPeriod = new TabControl();
            tabToday = new TabPage();
            tabYesterday = new TabPage();
            tabTomorrow = new TabPage();
            tabThisWeek = new TabPage();
            tabNextWeek = new TabPage();
            pnlCalendarDate = new Panel();
            txtCalendarDate = new TextBox();
            lstEconomicCalendar = new ListView();
            colTime = new ColumnHeader();
            colCountry = new ColumnHeader();
            colEventName = new ColumnHeader();
            pnlCalendarDetails = new TableLayoutPanel();
            lblActual = new Label();
            lblForecast = new Label();
            lblPrior = new Label();
            txtActual = new TextBox();
            txtForecast = new TextBox();
            txtPrior = new TextBox();
            pnlTitle.SuspendLayout();
            tabCalendarPeriod.SuspendLayout();
            pnlCalendarDate.SuspendLayout();
            pnlCalendarDetails.SuspendLayout();
            SuspendLayout();
            // 
            // pnlTitle
            // 
            pnlTitle.BackColor = Color.Black;
            pnlTitle.Controls.Add(lblCountryCodes);
            pnlTitle.Controls.Add(ddlCountryCodes);
            pnlTitle.Controls.Add(lblTitle);
            pnlTitle.Dock = DockStyle.Top;
            pnlTitle.Location = new Point(0, 0);
            pnlTitle.Margin = new Padding(4, 3, 4, 3);
            pnlTitle.Name = "pnlTitle";
            pnlTitle.Size = new Size(632, 25);
            pnlTitle.TabIndex = 0;
            // 
            // lblCountryCodes
            // 
            lblCountryCodes.AutoSize = true;
            lblCountryCodes.Dock = DockStyle.Right;
            lblCountryCodes.ForeColor = Color.White;
            lblCountryCodes.Location = new Point(502, 0);
            lblCountryCodes.Margin = new Padding(4, 0, 4, 0);
            lblCountryCodes.Name = "lblCountryCodes";
            lblCountryCodes.Padding = new Padding(0, 3, 0, 0);
            lblCountryCodes.Size = new Size(84, 18);
            lblCountryCodes.TabIndex = 2;
            lblCountryCodes.Text = "Country Code:";
            // 
            // ddlCountryCodes
            // 
            ddlCountryCodes.BackColor = Color.Black;
            ddlCountryCodes.Dock = DockStyle.Right;
            ddlCountryCodes.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlCountryCodes.ForeColor = Color.White;
            ddlCountryCodes.FormattingEnabled = true;
            ddlCountryCodes.Location = new Point(586, 0);
            ddlCountryCodes.Margin = new Padding(4, 3, 4, 3);
            ddlCountryCodes.Name = "ddlCountryCodes";
            ddlCountryCodes.Size = new Size(46, 23);
            ddlCountryCodes.TabIndex = 1;
            ddlCountryCodes.SelectedIndexChanged += ddlCountryCodes_SelectedIndexChanged;
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
            lblTitle.Size = new Size(156, 21);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Economic Calendar";
            // 
            // tabCalendarPeriod
            // 
            tabCalendarPeriod.Appearance = TabAppearance.Buttons;
            tabCalendarPeriod.Controls.Add(tabToday);
            tabCalendarPeriod.Controls.Add(tabYesterday);
            tabCalendarPeriod.Controls.Add(tabTomorrow);
            tabCalendarPeriod.Controls.Add(tabThisWeek);
            tabCalendarPeriod.Controls.Add(tabNextWeek);
            tabCalendarPeriod.Dock = DockStyle.Top;
            tabCalendarPeriod.Location = new Point(0, 25);
            tabCalendarPeriod.Margin = new Padding(4, 3, 4, 3);
            tabCalendarPeriod.Name = "tabCalendarPeriod";
            tabCalendarPeriod.SelectedIndex = 0;
            tabCalendarPeriod.Size = new Size(632, 21);
            tabCalendarPeriod.TabIndex = 1;
            tabCalendarPeriod.SelectedIndexChanged += tabCalendarPeriod_SelectedIndexChanged;
            // 
            // tabToday
            // 
            tabToday.Location = new Point(4, 27);
            tabToday.Margin = new Padding(4, 3, 4, 3);
            tabToday.Name = "tabToday";
            tabToday.Padding = new Padding(4, 3, 4, 3);
            tabToday.Size = new Size(624, 0);
            tabToday.TabIndex = 1;
            tabToday.Text = "Today";
            tabToday.UseVisualStyleBackColor = true;
            // 
            // tabYesterday
            // 
            tabYesterday.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            tabYesterday.Location = new Point(4, 27);
            tabYesterday.Margin = new Padding(4, 3, 4, 3);
            tabYesterday.Name = "tabYesterday";
            tabYesterday.Padding = new Padding(4, 3, 4, 3);
            tabYesterday.Size = new Size(624, 0);
            tabYesterday.TabIndex = 0;
            tabYesterday.Text = "Yesterday";
            tabYesterday.UseVisualStyleBackColor = true;
            // 
            // tabTomorrow
            // 
            tabTomorrow.Location = new Point(4, 27);
            tabTomorrow.Margin = new Padding(4, 3, 4, 3);
            tabTomorrow.Name = "tabTomorrow";
            tabTomorrow.Size = new Size(624, 0);
            tabTomorrow.TabIndex = 2;
            tabTomorrow.Text = "Tomorrow";
            tabTomorrow.UseVisualStyleBackColor = true;
            // 
            // tabThisWeek
            // 
            tabThisWeek.Location = new Point(4, 27);
            tabThisWeek.Margin = new Padding(4, 3, 4, 3);
            tabThisWeek.Name = "tabThisWeek";
            tabThisWeek.Size = new Size(624, 0);
            tabThisWeek.TabIndex = 3;
            tabThisWeek.Text = "This Week";
            tabThisWeek.UseVisualStyleBackColor = true;
            // 
            // tabNextWeek
            // 
            tabNextWeek.BackColor = Color.Black;
            tabNextWeek.Location = new Point(4, 27);
            tabNextWeek.Margin = new Padding(4, 3, 4, 3);
            tabNextWeek.Name = "tabNextWeek";
            tabNextWeek.Size = new Size(624, 0);
            tabNextWeek.TabIndex = 4;
            tabNextWeek.Text = "Next Week";
            tabNextWeek.UseVisualStyleBackColor = true;
            // 
            // pnlCalendarDate
            // 
            pnlCalendarDate.Controls.Add(txtCalendarDate);
            pnlCalendarDate.Dock = DockStyle.Top;
            pnlCalendarDate.Location = new Point(0, 46);
            pnlCalendarDate.Margin = new Padding(4, 3, 4, 3);
            pnlCalendarDate.Name = "pnlCalendarDate";
            pnlCalendarDate.Size = new Size(632, 17);
            pnlCalendarDate.TabIndex = 4;
            // 
            // txtCalendarDate
            // 
            txtCalendarDate.BackColor = Color.Black;
            txtCalendarDate.BorderStyle = BorderStyle.None;
            txtCalendarDate.Dock = DockStyle.Fill;
            txtCalendarDate.Font = new Font("Microsoft Sans Serif", 11.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtCalendarDate.ForeColor = Color.White;
            txtCalendarDate.Location = new Point(0, 0);
            txtCalendarDate.Margin = new Padding(4, 3, 4, 3);
            txtCalendarDate.Name = "txtCalendarDate";
            txtCalendarDate.Size = new Size(632, 17);
            txtCalendarDate.TabIndex = 0;
            txtCalendarDate.Text = "Monday, January 05, 2022";
            txtCalendarDate.TextAlign = HorizontalAlignment.Center;
            // 
            // lstEconomicCalendar
            // 
            lstEconomicCalendar.BackColor = Color.Black;
            lstEconomicCalendar.BorderStyle = BorderStyle.None;
            lstEconomicCalendar.Columns.AddRange(new ColumnHeader[] { colTime, colCountry, colEventName });
            lstEconomicCalendar.Dock = DockStyle.Top;
            lstEconomicCalendar.ForeColor = Color.White;
            lstEconomicCalendar.FullRowSelect = true;
            lstEconomicCalendar.HeaderStyle = ColumnHeaderStyle.None;
            lstEconomicCalendar.Location = new Point(0, 63);
            lstEconomicCalendar.Margin = new Padding(4, 3, 4, 3);
            lstEconomicCalendar.MultiSelect = false;
            lstEconomicCalendar.Name = "lstEconomicCalendar";
            lstEconomicCalendar.Size = new Size(632, 134);
            lstEconomicCalendar.TabIndex = 5;
            lstEconomicCalendar.UseCompatibleStateImageBehavior = false;
            lstEconomicCalendar.View = View.Details;
            lstEconomicCalendar.SelectedIndexChanged += lstEconomicCalendar_SelectedIndexChanged;
            // 
            // colTime
            // 
            colTime.Text = "";
            colTime.Width = 80;
            // 
            // colCountry
            // 
            colCountry.Text = "";
            colCountry.TextAlign = HorizontalAlignment.Center;
            colCountry.Width = 80;
            // 
            // colEventName
            // 
            colEventName.Text = "";
            colEventName.Width = 450;
            // 
            // pnlCalendarDetails
            // 
            pnlCalendarDetails.BackColor = Color.Black;
            pnlCalendarDetails.ColumnCount = 6;
            pnlCalendarDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66667F));
            pnlCalendarDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66667F));
            pnlCalendarDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18.45018F));
            pnlCalendarDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17.52768F));
            pnlCalendarDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13.83764F));
            pnlCalendarDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66667F));
            pnlCalendarDetails.Controls.Add(lblActual, 0, 0);
            pnlCalendarDetails.Controls.Add(lblForecast, 2, 0);
            pnlCalendarDetails.Controls.Add(lblPrior, 4, 0);
            pnlCalendarDetails.Controls.Add(txtActual, 1, 0);
            pnlCalendarDetails.Controls.Add(txtForecast, 3, 0);
            pnlCalendarDetails.Controls.Add(txtPrior, 5, 0);
            pnlCalendarDetails.Dock = DockStyle.Top;
            pnlCalendarDetails.ForeColor = Color.White;
            pnlCalendarDetails.Location = new Point(0, 197);
            pnlCalendarDetails.Margin = new Padding(4, 3, 4, 3);
            pnlCalendarDetails.Name = "pnlCalendarDetails";
            pnlCalendarDetails.RowCount = 1;
            pnlCalendarDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            pnlCalendarDetails.Size = new Size(632, 28);
            pnlCalendarDetails.TabIndex = 6;
            // 
            // lblActual
            // 
            lblActual.AutoSize = true;
            lblActual.Dock = DockStyle.Right;
            lblActual.Font = new Font("Microsoft Sans Serif", 11.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblActual.ForeColor = Color.White;
            lblActual.Location = new Point(47, 0);
            lblActual.Margin = new Padding(4, 0, 4, 0);
            lblActual.Name = "lblActual";
            lblActual.Padding = new Padding(0, 3, 0, 0);
            lblActual.Size = new Size(54, 28);
            lblActual.TabIndex = 1;
            lblActual.Text = "Actual";
            // 
            // lblForecast
            // 
            lblForecast.AutoSize = true;
            lblForecast.Font = new Font("Microsoft Sans Serif", 11.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblForecast.ForeColor = Color.White;
            lblForecast.Location = new Point(214, 0);
            lblForecast.Margin = new Padding(4, 0, 4, 0);
            lblForecast.Name = "lblForecast";
            lblForecast.Padding = new Padding(0, 3, 0, 0);
            lblForecast.Size = new Size(75, 21);
            lblForecast.TabIndex = 2;
            lblForecast.Text = "Forecast";
            // 
            // lblPrior
            // 
            lblPrior.AutoSize = true;
            lblPrior.Dock = DockStyle.Right;
            lblPrior.Font = new Font("Microsoft Sans Serif", 11.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblPrior.ForeColor = Color.White;
            lblPrior.Location = new Point(474, 0);
            lblPrior.Margin = new Padding(4, 0, 4, 0);
            lblPrior.Name = "lblPrior";
            lblPrior.Padding = new Padding(0, 3, 0, 0);
            lblPrior.Size = new Size(45, 28);
            lblPrior.TabIndex = 3;
            lblPrior.Text = "Prior";
            // 
            // txtActual
            // 
            txtActual.BackColor = Color.Black;
            txtActual.BorderStyle = BorderStyle.None;
            txtActual.ForeColor = Color.White;
            txtActual.Location = new Point(109, 3);
            txtActual.Margin = new Padding(4, 3, 4, 3);
            txtActual.Name = "txtActual";
            txtActual.ReadOnly = true;
            txtActual.Size = new Size(97, 16);
            txtActual.TabIndex = 4;
            txtActual.TextAlign = HorizontalAlignment.Center;
            // 
            // txtForecast
            // 
            txtForecast.BackColor = Color.Black;
            txtForecast.BorderStyle = BorderStyle.None;
            txtForecast.ForeColor = Color.White;
            txtForecast.Location = new Point(330, 3);
            txtForecast.Margin = new Padding(4, 3, 4, 3);
            txtForecast.Name = "txtForecast";
            txtForecast.ReadOnly = true;
            txtForecast.Size = new Size(102, 16);
            txtForecast.TabIndex = 5;
            txtForecast.TextAlign = HorizontalAlignment.Center;
            // 
            // txtPrior
            // 
            txtPrior.BackColor = Color.Black;
            txtPrior.BorderStyle = BorderStyle.None;
            txtPrior.ForeColor = Color.White;
            txtPrior.Location = new Point(527, 3);
            txtPrior.Margin = new Padding(4, 3, 4, 3);
            txtPrior.Name = "txtPrior";
            txtPrior.ReadOnly = true;
            txtPrior.Size = new Size(100, 16);
            txtPrior.TabIndex = 6;
            txtPrior.TextAlign = HorizontalAlignment.Center;
            // 
            // MarketEconomicCalendarView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(pnlCalendarDetails);
            Controls.Add(lstEconomicCalendar);
            Controls.Add(pnlCalendarDate);
            Controls.Add(tabCalendarPeriod);
            Controls.Add(pnlTitle);
            Margin = new Padding(4, 3, 4, 3);
            Name = "MarketEconomicCalendarView";
            Size = new Size(632, 228);
            pnlTitle.ResumeLayout(false);
            pnlTitle.PerformLayout();
            tabCalendarPeriod.ResumeLayout(false);
            pnlCalendarDate.ResumeLayout(false);
            pnlCalendarDate.PerformLayout();
            pnlCalendarDetails.ResumeLayout(false);
            pnlCalendarDetails.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlTitle;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.TabControl tabCalendarPeriod;
        private System.Windows.Forms.TabPage tabYesterday;
        private System.Windows.Forms.TabPage tabToday;
        private System.Windows.Forms.TabPage tabTomorrow;
        private System.Windows.Forms.TabPage tabThisWeek;
        private System.Windows.Forms.TabPage tabNextWeek;
        private System.Windows.Forms.Panel pnlCalendarDate;
        private System.Windows.Forms.TextBox txtCalendarDate;
        private System.Windows.Forms.ListView lstEconomicCalendar;
        private System.Windows.Forms.ColumnHeader colTime;
        private System.Windows.Forms.ColumnHeader colCountry;
        private System.Windows.Forms.ColumnHeader colEventName;
        private System.Windows.Forms.TableLayoutPanel pnlCalendarDetails;
        private System.Windows.Forms.Label lblActual;
        private System.Windows.Forms.Label lblForecast;
        private System.Windows.Forms.Label lblPrior;
        private System.Windows.Forms.TextBox txtActual;
        private System.Windows.Forms.TextBox txtForecast;
        private System.Windows.Forms.TextBox txtPrior;
        private System.Windows.Forms.Label lblCountryCodes;
        private System.Windows.Forms.ComboBox ddlCountryCodes;
    }
}
