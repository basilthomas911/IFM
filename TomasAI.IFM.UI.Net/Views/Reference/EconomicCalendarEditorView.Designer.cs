namespace TomasAI.IFM.UI.Net.Views.Reference
{
    partial class EconomicCalendarEditorView
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
            splitContainer1 = new SplitContainer();
            lstCalendarEvents = new ListBox();
            pnlEconomicCalendarEvents = new Panel();
            label2 = new Label();
            tlpCalendarEvents = new TableLayoutPanel();
            pnlContractId = new Panel();
            lblEventDate = new Label();
            pnlDescription = new Panel();
            lblCurrency = new Label();
            pnlContractMonth = new Panel();
            lblEventName = new Label();
            pnlSymbol = new Panel();
            lblActual = new Label();
            pnlLocalSymbol = new Panel();
            lblForecast = new Label();
            pnlSecurityType = new Panel();
            dtmEventDate = new DateTimePicker();
            txtEventName = new TextBox();
            pnlPrior = new Panel();
            label1 = new Label();
            ddlCountryCodes = new ComboBox();
            txtActual = new TextBox();
            txtForecast = new TextBox();
            txtPrior = new TextBox();
            lblLastTradeDate = new Label();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            pnlEconomicCalendarEvents.SuspendLayout();
            tlpCalendarEvents.SuspendLayout();
            pnlContractId.SuspendLayout();
            pnlDescription.SuspendLayout();
            pnlContractMonth.SuspendLayout();
            pnlSymbol.SuspendLayout();
            pnlLocalSymbol.SuspendLayout();
            pnlPrior.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Margin = new Padding(2);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(lstCalendarEvents);
            splitContainer1.Panel1.Controls.Add(pnlEconomicCalendarEvents);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(tlpCalendarEvents);
            splitContainer1.Size = new Size(1031, 468);
            splitContainer1.SplitterDistance = 521;
            splitContainer1.TabIndex = 0;
            // 
            // lstCalendarEvents
            // 
            lstCalendarEvents.BackColor = Color.FromArgb(64, 64, 64);
            lstCalendarEvents.BorderStyle = BorderStyle.FixedSingle;
            lstCalendarEvents.Dock = DockStyle.Fill;
            lstCalendarEvents.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lstCalendarEvents.ForeColor = Color.White;
            lstCalendarEvents.FormattingEnabled = true;
            lstCalendarEvents.Location = new Point(0, 30);
            lstCalendarEvents.Margin = new Padding(2);
            lstCalendarEvents.Name = "lstCalendarEvents";
            lstCalendarEvents.Size = new Size(521, 438);
            lstCalendarEvents.TabIndex = 3;
            lstCalendarEvents.SelectedIndexChanged += lstCalendarEvents_SelectedIndexChanged;
            // 
            // pnlEconomicCalendarEvents
            // 
            pnlEconomicCalendarEvents.BackColor = Color.Gray;
            pnlEconomicCalendarEvents.Controls.Add(label2);
            pnlEconomicCalendarEvents.Dock = DockStyle.Top;
            pnlEconomicCalendarEvents.Location = new Point(0, 0);
            pnlEconomicCalendarEvents.Margin = new Padding(4, 3, 4, 3);
            pnlEconomicCalendarEvents.Name = "pnlEconomicCalendarEvents";
            pnlEconomicCalendarEvents.Size = new Size(521, 30);
            pnlEconomicCalendarEvents.TabIndex = 2;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label2.ForeColor = Color.White;
            label2.Location = new Point(160, 5);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(177, 17);
            label2.TabIndex = 0;
            label2.Text = "Economic Calendar Events";
            // 
            // tlpCalendarEvents
            // 
            tlpCalendarEvents.BackColor = Color.FromArgb(64, 64, 64);
            tlpCalendarEvents.ColumnCount = 2;
            tlpCalendarEvents.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28.14371F));
            tlpCalendarEvents.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 71.85629F));
            tlpCalendarEvents.Controls.Add(pnlContractId, 0, 0);
            tlpCalendarEvents.Controls.Add(pnlDescription, 0, 1);
            tlpCalendarEvents.Controls.Add(pnlContractMonth, 0, 2);
            tlpCalendarEvents.Controls.Add(pnlSymbol, 0, 3);
            tlpCalendarEvents.Controls.Add(pnlLocalSymbol, 0, 4);
            tlpCalendarEvents.Controls.Add(pnlSecurityType, 0, 6);
            tlpCalendarEvents.Controls.Add(dtmEventDate, 1, 0);
            tlpCalendarEvents.Controls.Add(txtEventName, 1, 2);
            tlpCalendarEvents.Controls.Add(pnlPrior, 0, 5);
            tlpCalendarEvents.Controls.Add(ddlCountryCodes, 1, 1);
            tlpCalendarEvents.Controls.Add(txtActual, 1, 3);
            tlpCalendarEvents.Controls.Add(txtForecast, 1, 4);
            tlpCalendarEvents.Controls.Add(txtPrior, 1, 5);
            tlpCalendarEvents.Dock = DockStyle.Fill;
            tlpCalendarEvents.Location = new Point(0, 0);
            tlpCalendarEvents.Margin = new Padding(2);
            tlpCalendarEvents.Name = "tlpCalendarEvents";
            tlpCalendarEvents.RowCount = 5;
            tlpCalendarEvents.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tlpCalendarEvents.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tlpCalendarEvents.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tlpCalendarEvents.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tlpCalendarEvents.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tlpCalendarEvents.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tlpCalendarEvents.RowStyles.Add(new RowStyle(SizeType.Absolute, 29F));
            tlpCalendarEvents.Size = new Size(506, 468);
            tlpCalendarEvents.TabIndex = 0;
            // 
            // pnlContractId
            // 
            pnlContractId.Controls.Add(lblEventDate);
            pnlContractId.Dock = DockStyle.Fill;
            pnlContractId.Location = new Point(0, 0);
            pnlContractId.Margin = new Padding(0);
            pnlContractId.Name = "pnlContractId";
            pnlContractId.Size = new Size(142, 35);
            pnlContractId.TabIndex = 0;
            // 
            // lblEventDate
            // 
            lblEventDate.AutoSize = true;
            lblEventDate.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblEventDate.ForeColor = Color.White;
            lblEventDate.Location = new Point(44, 4);
            lblEventDate.Margin = new Padding(2, 0, 2, 0);
            lblEventDate.Name = "lblEventDate";
            lblEventDate.Size = new Size(82, 17);
            lblEventDate.TabIndex = 0;
            lblEventDate.Text = "Event Date:";
            lblEventDate.TextAlign = ContentAlignment.MiddleRight;
            // 
            // pnlDescription
            // 
            pnlDescription.Controls.Add(lblCurrency);
            pnlDescription.Dock = DockStyle.Fill;
            pnlDescription.Location = new Point(2, 37);
            pnlDescription.Margin = new Padding(2);
            pnlDescription.Name = "pnlDescription";
            pnlDescription.Size = new Size(138, 31);
            pnlDescription.TabIndex = 2;
            // 
            // lblCurrency
            // 
            lblCurrency.AutoSize = true;
            lblCurrency.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblCurrency.ForeColor = Color.White;
            lblCurrency.Location = new Point(54, 4);
            lblCurrency.Margin = new Padding(2, 0, 2, 0);
            lblCurrency.Name = "lblCurrency";
            lblCurrency.Size = new Size(69, 17);
            lblCurrency.TabIndex = 0;
            lblCurrency.Text = "Currency:";
            lblCurrency.TextAlign = ContentAlignment.MiddleRight;
            // 
            // pnlContractMonth
            // 
            pnlContractMonth.Controls.Add(lblEventName);
            pnlContractMonth.Dock = DockStyle.Fill;
            pnlContractMonth.Location = new Point(2, 72);
            pnlContractMonth.Margin = new Padding(2);
            pnlContractMonth.Name = "pnlContractMonth";
            pnlContractMonth.Size = new Size(138, 31);
            pnlContractMonth.TabIndex = 4;
            // 
            // lblEventName
            // 
            lblEventName.AutoSize = true;
            lblEventName.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblEventName.ForeColor = Color.White;
            lblEventName.Location = new Point(34, 3);
            lblEventName.Margin = new Padding(2, 0, 2, 0);
            lblEventName.Name = "lblEventName";
            lblEventName.Size = new Size(89, 17);
            lblEventName.TabIndex = 0;
            lblEventName.Text = "Event Name:";
            lblEventName.TextAlign = ContentAlignment.MiddleRight;
            // 
            // pnlSymbol
            // 
            pnlSymbol.Controls.Add(lblActual);
            pnlSymbol.Dock = DockStyle.Fill;
            pnlSymbol.Location = new Point(2, 107);
            pnlSymbol.Margin = new Padding(2);
            pnlSymbol.Name = "pnlSymbol";
            pnlSymbol.Size = new Size(138, 31);
            pnlSymbol.TabIndex = 10;
            // 
            // lblActual
            // 
            lblActual.AutoSize = true;
            lblActual.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblActual.ForeColor = Color.White;
            lblActual.Location = new Point(73, 2);
            lblActual.Margin = new Padding(2, 0, 2, 0);
            lblActual.Name = "lblActual";
            lblActual.Size = new Size(51, 17);
            lblActual.TabIndex = 0;
            lblActual.Text = "Actual:";
            lblActual.TextAlign = ContentAlignment.MiddleRight;
            // 
            // pnlLocalSymbol
            // 
            pnlLocalSymbol.Controls.Add(lblForecast);
            pnlLocalSymbol.Dock = DockStyle.Fill;
            pnlLocalSymbol.Location = new Point(2, 142);
            pnlLocalSymbol.Margin = new Padding(2);
            pnlLocalSymbol.Name = "pnlLocalSymbol";
            pnlLocalSymbol.Size = new Size(138, 31);
            pnlLocalSymbol.TabIndex = 12;
            // 
            // lblForecast
            // 
            lblForecast.AutoSize = true;
            lblForecast.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblForecast.ForeColor = Color.White;
            lblForecast.Location = new Point(57, 2);
            lblForecast.Margin = new Padding(2, 0, 2, 0);
            lblForecast.Name = "lblForecast";
            lblForecast.Size = new Size(67, 17);
            lblForecast.TabIndex = 0;
            lblForecast.Text = "Forecast:";
            // 
            // pnlSecurityType
            // 
            pnlSecurityType.Dock = DockStyle.Fill;
            pnlSecurityType.Location = new Point(2, 212);
            pnlSecurityType.Margin = new Padding(2);
            pnlSecurityType.Name = "pnlSecurityType";
            pnlSecurityType.Size = new Size(138, 254);
            pnlSecurityType.TabIndex = 14;
            // 
            // dtmEventDate
            // 
            dtmEventDate.CustomFormat = "yyyy-MM-dd hh:mm tt";
            dtmEventDate.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dtmEventDate.Format = DateTimePickerFormat.Custom;
            dtmEventDate.Location = new Point(146, 3);
            dtmEventDate.Margin = new Padding(4, 3, 4, 3);
            dtmEventDate.Name = "dtmEventDate";
            dtmEventDate.Size = new Size(228, 23);
            dtmEventDate.TabIndex = 24;
            dtmEventDate.ValueChanged += dtmEventDate_ValueChanged;
            // 
            // txtEventName
            // 
            txtEventName.BackColor = Color.Black;
            txtEventName.BorderStyle = BorderStyle.FixedSingle;
            txtEventName.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtEventName.ForeColor = Color.White;
            txtEventName.Location = new Point(146, 73);
            txtEventName.Margin = new Padding(4, 3, 4, 3);
            txtEventName.Name = "txtEventName";
            txtEventName.ReadOnly = true;
            txtEventName.Size = new Size(356, 23);
            txtEventName.TabIndex = 26;
            // 
            // pnlPrior
            // 
            pnlPrior.Controls.Add(label1);
            pnlPrior.Dock = DockStyle.Fill;
            pnlPrior.Location = new Point(4, 178);
            pnlPrior.Margin = new Padding(4, 3, 4, 3);
            pnlPrior.Name = "pnlPrior";
            pnlPrior.Size = new Size(134, 29);
            pnlPrior.TabIndex = 27;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label1.ForeColor = Color.White;
            label1.Location = new Point(83, 2);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(38, 16);
            label1.TabIndex = 0;
            label1.Text = "Prior:";
            // 
            // ddlCountryCodes
            // 
            ddlCountryCodes.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlCountryCodes.Enabled = false;
            ddlCountryCodes.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlCountryCodes.FormattingEnabled = true;
            ddlCountryCodes.Location = new Point(144, 37);
            ddlCountryCodes.Margin = new Padding(2);
            ddlCountryCodes.Name = "ddlCountryCodes";
            ddlCountryCodes.Size = new Size(229, 24);
            ddlCountryCodes.TabIndex = 25;
            ddlCountryCodes.SelectedIndexChanged += ddlCountryCodes_SelectedIndexChanged;
            // 
            // txtActual
            // 
            txtActual.BackColor = Color.Black;
            txtActual.BorderStyle = BorderStyle.FixedSingle;
            txtActual.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtActual.ForeColor = Color.White;
            txtActual.Location = new Point(146, 108);
            txtActual.Margin = new Padding(4, 3, 4, 3);
            txtActual.Name = "txtActual";
            txtActual.Size = new Size(228, 23);
            txtActual.TabIndex = 28;
            txtActual.TextChanged += txtActual_TextChanged;
            // 
            // txtForecast
            // 
            txtForecast.BackColor = Color.Black;
            txtForecast.BorderStyle = BorderStyle.FixedSingle;
            txtForecast.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtForecast.ForeColor = Color.White;
            txtForecast.Location = new Point(146, 143);
            txtForecast.Margin = new Padding(4, 3, 4, 3);
            txtForecast.Name = "txtForecast";
            txtForecast.Size = new Size(229, 23);
            txtForecast.TabIndex = 29;
            // 
            // txtPrior
            // 
            txtPrior.BackColor = Color.Black;
            txtPrior.BorderStyle = BorderStyle.FixedSingle;
            txtPrior.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtPrior.ForeColor = Color.White;
            txtPrior.Location = new Point(146, 178);
            txtPrior.Margin = new Padding(4, 3, 4, 3);
            txtPrior.Name = "txtPrior";
            txtPrior.Size = new Size(228, 23);
            txtPrior.TabIndex = 30;
            // 
            // lblLastTradeDate
            // 
            lblLastTradeDate.AutoSize = true;
            lblLastTradeDate.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblLastTradeDate.ForeColor = SystemColors.ControlLightLight;
            lblLastTradeDate.Location = new Point(11, 1);
            lblLastTradeDate.Margin = new Padding(2, 0, 2, 0);
            lblLastTradeDate.Name = "lblLastTradeDate";
            lblLastTradeDate.Size = new Size(115, 17);
            lblLastTradeDate.TabIndex = 0;
            lblLastTradeDate.Text = "Last Trade Date:";
            lblLastTradeDate.TextAlign = ContentAlignment.MiddleRight;
            // 
            // EconomicCalendarEditorView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            Controls.Add(splitContainer1);
            Margin = new Padding(2);
            Name = "EconomicCalendarEditorView";
            Size = new Size(1031, 468);
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            pnlEconomicCalendarEvents.ResumeLayout(false);
            pnlEconomicCalendarEvents.PerformLayout();
            tlpCalendarEvents.ResumeLayout(false);
            tlpCalendarEvents.PerformLayout();
            pnlContractId.ResumeLayout(false);
            pnlContractId.PerformLayout();
            pnlDescription.ResumeLayout(false);
            pnlDescription.PerformLayout();
            pnlContractMonth.ResumeLayout(false);
            pnlContractMonth.PerformLayout();
            pnlSymbol.ResumeLayout(false);
            pnlSymbol.PerformLayout();
            pnlLocalSymbol.ResumeLayout(false);
            pnlLocalSymbol.PerformLayout();
            pnlPrior.ResumeLayout(false);
            pnlPrior.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tlpCalendarEvents;
        private System.Windows.Forms.Panel pnlContractId;
        private System.Windows.Forms.Label lblEventDate;
        private System.Windows.Forms.Panel pnlDescription;
        private System.Windows.Forms.Panel pnlContractMonth;
        private System.Windows.Forms.Label lblLastTradeDate;
        private System.Windows.Forms.Panel pnlSymbol;
        private System.Windows.Forms.Label lblActual;
        private System.Windows.Forms.Panel pnlLocalSymbol;
        private System.Windows.Forms.Label lblForecast;
        private System.Windows.Forms.Panel pnlSecurityType;
        private System.Windows.Forms.Label lblCurrency;
        private System.Windows.Forms.ComboBox ddlCountryCodes;
        private System.Windows.Forms.DateTimePicker dtmEventDate;
        private System.Windows.Forms.Label lblEventName;
        private System.Windows.Forms.TextBox txtEventName;
        private System.Windows.Forms.Panel pnlPrior;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtActual;
        private System.Windows.Forms.TextBox txtForecast;
        private System.Windows.Forms.TextBox txtPrior;
        private System.Windows.Forms.ListBox lstCalendarEvents;
        private System.Windows.Forms.Panel pnlEconomicCalendarEvents;
        private System.Windows.Forms.Label label2;
    }
}
