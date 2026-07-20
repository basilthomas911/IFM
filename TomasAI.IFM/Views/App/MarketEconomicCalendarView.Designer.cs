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
            this.pnlTitle = new System.Windows.Forms.Panel();
            this.lblCountryCodes = new System.Windows.Forms.Label();
            this.ddlCountryCodes = new System.Windows.Forms.ComboBox();
            this.lblTitle = new System.Windows.Forms.Label();
            this.tabCalendarPeriod = new System.Windows.Forms.TabControl();
            this.tabToday = new System.Windows.Forms.TabPage();
            this.tabYesterday = new System.Windows.Forms.TabPage();
            this.tabTomorrow = new System.Windows.Forms.TabPage();
            this.tabThisWeek = new System.Windows.Forms.TabPage();
            this.tabNextWeek = new System.Windows.Forms.TabPage();
            this.pnlCalendarDate = new System.Windows.Forms.Panel();
            this.txtCalendarDate = new System.Windows.Forms.TextBox();
            this.lstEconomicCalendar = new System.Windows.Forms.ListView();
            this.colTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colCountry = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colEventName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.pnlCalendarDetails = new System.Windows.Forms.TableLayoutPanel();
            this.lblActual = new System.Windows.Forms.Label();
            this.lblForecast = new System.Windows.Forms.Label();
            this.lblPrior = new System.Windows.Forms.Label();
            this.txtActual = new System.Windows.Forms.TextBox();
            this.txtForecast = new System.Windows.Forms.TextBox();
            this.txtPrior = new System.Windows.Forms.TextBox();
            this.pnlTitle.SuspendLayout();
            this.tabCalendarPeriod.SuspendLayout();
            this.pnlCalendarDate.SuspendLayout();
            this.pnlCalendarDetails.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlTitle
            // 
            this.pnlTitle.BackColor = System.Drawing.Color.Black;
            this.pnlTitle.Controls.Add(this.lblCountryCodes);
            this.pnlTitle.Controls.Add(this.ddlCountryCodes);
            this.pnlTitle.Controls.Add(this.lblTitle);
            this.pnlTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitle.Location = new System.Drawing.Point(0, 0);
            this.pnlTitle.Name = "pnlTitle";
            this.pnlTitle.Size = new System.Drawing.Size(542, 22);
            this.pnlTitle.TabIndex = 0;
            // 
            // lblCountryCodes
            // 
            this.lblCountryCodes.AutoSize = true;
            this.lblCountryCodes.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblCountryCodes.ForeColor = System.Drawing.Color.White;
            this.lblCountryCodes.Location = new System.Drawing.Point(428, 0);
            this.lblCountryCodes.Name = "lblCountryCodes";
            this.lblCountryCodes.Padding = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.lblCountryCodes.Size = new System.Drawing.Size(74, 16);
            this.lblCountryCodes.TabIndex = 2;
            this.lblCountryCodes.Text = "Country Code:";
            // 
            // ddlCountryCodes
            // 
            this.ddlCountryCodes.BackColor = System.Drawing.Color.Black;
            this.ddlCountryCodes.Dock = System.Windows.Forms.DockStyle.Right;
            this.ddlCountryCodes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlCountryCodes.ForeColor = System.Drawing.Color.White;
            this.ddlCountryCodes.FormattingEnabled = true;
            this.ddlCountryCodes.Location = new System.Drawing.Point(502, 0);
            this.ddlCountryCodes.Name = "ddlCountryCodes";
            this.ddlCountryCodes.Size = new System.Drawing.Size(40, 21);
            this.ddlCountryCodes.TabIndex = 1;
            this.ddlCountryCodes.SelectedIndexChanged += new System.EventHandler(this.ddlCountryCodes_SelectedIndexChanged);
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.BackColor = System.Drawing.Color.Black;
            this.lblTitle.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Location = new System.Drawing.Point(0, 0);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Padding = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.lblTitle.Size = new System.Drawing.Size(156, 21);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Economic Calendar";
            // 
            // tabCalendarPeriod
            // 
            this.tabCalendarPeriod.Appearance = System.Windows.Forms.TabAppearance.Buttons;
            this.tabCalendarPeriod.Controls.Add(this.tabToday);
            this.tabCalendarPeriod.Controls.Add(this.tabYesterday);
            this.tabCalendarPeriod.Controls.Add(this.tabTomorrow);
            this.tabCalendarPeriod.Controls.Add(this.tabThisWeek);
            this.tabCalendarPeriod.Controls.Add(this.tabNextWeek);
            this.tabCalendarPeriod.Dock = System.Windows.Forms.DockStyle.Top;
            this.tabCalendarPeriod.Location = new System.Drawing.Point(0, 22);
            this.tabCalendarPeriod.Name = "tabCalendarPeriod";
            this.tabCalendarPeriod.SelectedIndex = 0;
            this.tabCalendarPeriod.Size = new System.Drawing.Size(542, 18);
            this.tabCalendarPeriod.TabIndex = 1;
            this.tabCalendarPeriod.SelectedIndexChanged += new System.EventHandler(this.tabCalendarPeriod_SelectedIndexChanged);
            // 
            // tabToday
            // 
            this.tabToday.Location = new System.Drawing.Point(4, 25);
            this.tabToday.Name = "tabToday";
            this.tabToday.Padding = new System.Windows.Forms.Padding(3);
            this.tabToday.Size = new System.Drawing.Size(534, 0);
            this.tabToday.TabIndex = 1;
            this.tabToday.Text = "Today";
            this.tabToday.UseVisualStyleBackColor = true;
            // 
            // tabYesterday
            // 
            this.tabYesterday.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabYesterday.Location = new System.Drawing.Point(4, 25);
            this.tabYesterday.Name = "tabYesterday";
            this.tabYesterday.Padding = new System.Windows.Forms.Padding(3);
            this.tabYesterday.Size = new System.Drawing.Size(534, 0);
            this.tabYesterday.TabIndex = 0;
            this.tabYesterday.Text = "Yesterday";
            this.tabYesterday.UseVisualStyleBackColor = true;
            // 
            // tabTomorrow
            // 
            this.tabTomorrow.Location = new System.Drawing.Point(4, 25);
            this.tabTomorrow.Name = "tabTomorrow";
            this.tabTomorrow.Size = new System.Drawing.Size(534, 0);
            this.tabTomorrow.TabIndex = 2;
            this.tabTomorrow.Text = "Tomorrow";
            this.tabTomorrow.UseVisualStyleBackColor = true;
            // 
            // tabThisWeek
            // 
            this.tabThisWeek.Location = new System.Drawing.Point(4, 25);
            this.tabThisWeek.Name = "tabThisWeek";
            this.tabThisWeek.Size = new System.Drawing.Size(534, 0);
            this.tabThisWeek.TabIndex = 3;
            this.tabThisWeek.Text = "This Week";
            this.tabThisWeek.UseVisualStyleBackColor = true;
            // 
            // tabNextWeek
            // 
            this.tabNextWeek.BackColor = System.Drawing.Color.Black;
            this.tabNextWeek.Location = new System.Drawing.Point(4, 25);
            this.tabNextWeek.Name = "tabNextWeek";
            this.tabNextWeek.Size = new System.Drawing.Size(534, 0);
            this.tabNextWeek.TabIndex = 4;
            this.tabNextWeek.Text = "Next Week";
            this.tabNextWeek.UseVisualStyleBackColor = true;
            // 
            // pnlCalendarDate
            // 
            this.pnlCalendarDate.Controls.Add(this.txtCalendarDate);
            this.pnlCalendarDate.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCalendarDate.Location = new System.Drawing.Point(0, 40);
            this.pnlCalendarDate.Name = "pnlCalendarDate";
            this.pnlCalendarDate.Size = new System.Drawing.Size(542, 15);
            this.pnlCalendarDate.TabIndex = 4;
            // 
            // txtCalendarDate
            // 
            this.txtCalendarDate.BackColor = System.Drawing.Color.Black;
            this.txtCalendarDate.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtCalendarDate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCalendarDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtCalendarDate.ForeColor = System.Drawing.Color.White;
            this.txtCalendarDate.Location = new System.Drawing.Point(0, 0);
            this.txtCalendarDate.Name = "txtCalendarDate";
            this.txtCalendarDate.Size = new System.Drawing.Size(542, 17);
            this.txtCalendarDate.TabIndex = 0;
            this.txtCalendarDate.Text = "Monday, January 05, 2022";
            this.txtCalendarDate.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lstEconomicCalendar
            // 
            this.lstEconomicCalendar.BackColor = System.Drawing.Color.Black;
            this.lstEconomicCalendar.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lstEconomicCalendar.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colTime,
            this.colCountry,
            this.colEventName});
            this.lstEconomicCalendar.Dock = System.Windows.Forms.DockStyle.Top;
            this.lstEconomicCalendar.ForeColor = System.Drawing.Color.White;
            this.lstEconomicCalendar.FullRowSelect = true;
            this.lstEconomicCalendar.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lstEconomicCalendar.HideSelection = false;
            this.lstEconomicCalendar.Location = new System.Drawing.Point(0, 55);
            this.lstEconomicCalendar.MultiSelect = false;
            this.lstEconomicCalendar.Name = "lstEconomicCalendar";
            this.lstEconomicCalendar.Size = new System.Drawing.Size(542, 116);
            this.lstEconomicCalendar.TabIndex = 5;
            this.lstEconomicCalendar.UseCompatibleStateImageBehavior = false;
            this.lstEconomicCalendar.View = System.Windows.Forms.View.Details;
            this.lstEconomicCalendar.SelectedIndexChanged += new System.EventHandler(this.lstEconomicCalendar_SelectedIndexChanged);
            // 
            // colTime
            // 
            this.colTime.Text = "";
            this.colTime.Width = 80;
            // 
            // colCountry
            // 
            this.colCountry.Text = "";
            this.colCountry.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.colCountry.Width = 80;
            // 
            // colEventName
            // 
            this.colEventName.Text = "";
            this.colEventName.Width = 450;
            // 
            // pnlCalendarDetails
            // 
            this.pnlCalendarDetails.BackColor = System.Drawing.Color.Black;
            this.pnlCalendarDetails.ColumnCount = 6;
            this.pnlCalendarDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.pnlCalendarDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.pnlCalendarDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18.45018F));
            this.pnlCalendarDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17.52768F));
            this.pnlCalendarDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 13.83764F));
            this.pnlCalendarDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.pnlCalendarDetails.Controls.Add(this.lblActual, 0, 0);
            this.pnlCalendarDetails.Controls.Add(this.lblForecast, 2, 0);
            this.pnlCalendarDetails.Controls.Add(this.lblPrior, 4, 0);
            this.pnlCalendarDetails.Controls.Add(this.txtActual, 1, 0);
            this.pnlCalendarDetails.Controls.Add(this.txtForecast, 3, 0);
            this.pnlCalendarDetails.Controls.Add(this.txtPrior, 5, 0);
            this.pnlCalendarDetails.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCalendarDetails.ForeColor = System.Drawing.Color.White;
            this.pnlCalendarDetails.Location = new System.Drawing.Point(0, 171);
            this.pnlCalendarDetails.Name = "pnlCalendarDetails";
            this.pnlCalendarDetails.RowCount = 1;
            this.pnlCalendarDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.pnlCalendarDetails.Size = new System.Drawing.Size(542, 24);
            this.pnlCalendarDetails.TabIndex = 6;
            // 
            // lblActual
            // 
            this.lblActual.AutoSize = true;
            this.lblActual.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblActual.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblActual.ForeColor = System.Drawing.Color.White;
            this.lblActual.Location = new System.Drawing.Point(33, 0);
            this.lblActual.Name = "lblActual";
            this.lblActual.Padding = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.lblActual.Size = new System.Drawing.Size(54, 24);
            this.lblActual.TabIndex = 1;
            this.lblActual.Text = "Actual";
            // 
            // lblForecast
            // 
            this.lblForecast.AutoSize = true;
            this.lblForecast.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblForecast.ForeColor = System.Drawing.Color.White;
            this.lblForecast.Location = new System.Drawing.Point(183, 0);
            this.lblForecast.Name = "lblForecast";
            this.lblForecast.Padding = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.lblForecast.Size = new System.Drawing.Size(75, 21);
            this.lblForecast.TabIndex = 2;
            this.lblForecast.Text = "Forecast";
            // 
            // lblPrior
            // 
            this.lblPrior.AutoSize = true;
            this.lblPrior.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblPrior.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPrior.ForeColor = System.Drawing.Color.White;
            this.lblPrior.Location = new System.Drawing.Point(402, 0);
            this.lblPrior.Name = "lblPrior";
            this.lblPrior.Padding = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.lblPrior.Size = new System.Drawing.Size(45, 24);
            this.lblPrior.TabIndex = 3;
            this.lblPrior.Text = "Prior";
            // 
            // txtActual
            // 
            this.txtActual.BackColor = System.Drawing.Color.Black;
            this.txtActual.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtActual.ForeColor = System.Drawing.Color.White;
            this.txtActual.Location = new System.Drawing.Point(93, 3);
            this.txtActual.Name = "txtActual";
            this.txtActual.ReadOnly = true;
            this.txtActual.Size = new System.Drawing.Size(84, 13);
            this.txtActual.TabIndex = 4;
            this.txtActual.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtForecast
            // 
            this.txtForecast.BackColor = System.Drawing.Color.Black;
            this.txtForecast.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtForecast.ForeColor = System.Drawing.Color.White;
            this.txtForecast.Location = new System.Drawing.Point(283, 3);
            this.txtForecast.Name = "txtForecast";
            this.txtForecast.ReadOnly = true;
            this.txtForecast.Size = new System.Drawing.Size(89, 13);
            this.txtForecast.TabIndex = 5;
            this.txtForecast.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtPrior
            // 
            this.txtPrior.BackColor = System.Drawing.Color.Black;
            this.txtPrior.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtPrior.ForeColor = System.Drawing.Color.White;
            this.txtPrior.Location = new System.Drawing.Point(453, 3);
            this.txtPrior.Name = "txtPrior";
            this.txtPrior.ReadOnly = true;
            this.txtPrior.Size = new System.Drawing.Size(86, 13);
            this.txtPrior.TabIndex = 6;
            this.txtPrior.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // MarketEconomicCalendarView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pnlCalendarDetails);
            this.Controls.Add(this.lstEconomicCalendar);
            this.Controls.Add(this.pnlCalendarDate);
            this.Controls.Add(this.tabCalendarPeriod);
            this.Controls.Add(this.pnlTitle);
            this.Name = "MarketEconomicCalendarView";
            this.Size = new System.Drawing.Size(542, 198);
            this.pnlTitle.ResumeLayout(false);
            this.pnlTitle.PerformLayout();
            this.tabCalendarPeriod.ResumeLayout(false);
            this.pnlCalendarDate.ResumeLayout(false);
            this.pnlCalendarDate.PerformLayout();
            this.pnlCalendarDetails.ResumeLayout(false);
            this.pnlCalendarDetails.PerformLayout();
            this.ResumeLayout(false);

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
