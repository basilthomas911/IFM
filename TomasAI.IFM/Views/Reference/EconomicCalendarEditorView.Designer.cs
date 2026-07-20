namespace TomasAI.IFM.Views.Reference
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lstCalendarEvents = new System.Windows.Forms.ListBox();
            this.pnlEconomicCalendarEvents = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.tlpCalendarEvents = new System.Windows.Forms.TableLayoutPanel();
            this.pnlContractId = new System.Windows.Forms.Panel();
            this.lblEventDate = new System.Windows.Forms.Label();
            this.pnlDescription = new System.Windows.Forms.Panel();
            this.lblCurrency = new System.Windows.Forms.Label();
            this.pnlContractMonth = new System.Windows.Forms.Panel();
            this.lblEventName = new System.Windows.Forms.Label();
            this.pnlSymbol = new System.Windows.Forms.Panel();
            this.lblActual = new System.Windows.Forms.Label();
            this.pnlLocalSymbol = new System.Windows.Forms.Panel();
            this.lblForecast = new System.Windows.Forms.Label();
            this.pnlSecurityType = new System.Windows.Forms.Panel();
            this.dtmEventDate = new System.Windows.Forms.DateTimePicker();
            this.txtEventName = new System.Windows.Forms.TextBox();
            this.pnlPrior = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.ddlCountryCodes = new System.Windows.Forms.ComboBox();
            this.txtActual = new System.Windows.Forms.TextBox();
            this.txtForecast = new System.Windows.Forms.TextBox();
            this.txtPrior = new System.Windows.Forms.TextBox();
            this.lblLastTradeDate = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.pnlEconomicCalendarEvents.SuspendLayout();
            this.tlpCalendarEvents.SuspendLayout();
            this.pnlContractId.SuspendLayout();
            this.pnlDescription.SuspendLayout();
            this.pnlContractMonth.SuspendLayout();
            this.pnlSymbol.SuspendLayout();
            this.pnlLocalSymbol.SuspendLayout();
            this.pnlPrior.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.lstCalendarEvents);
            this.splitContainer1.Panel1.Controls.Add(this.pnlEconomicCalendarEvents);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.tlpCalendarEvents);
            this.splitContainer1.Size = new System.Drawing.Size(884, 406);
            this.splitContainer1.SplitterDistance = 447;
            this.splitContainer1.SplitterWidth = 3;
            this.splitContainer1.TabIndex = 0;
            // 
            // lstCalendarEvents
            // 
            this.lstCalendarEvents.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lstCalendarEvents.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lstCalendarEvents.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstCalendarEvents.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstCalendarEvents.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lstCalendarEvents.FormattingEnabled = true;
            this.lstCalendarEvents.ItemHeight = 16;
            this.lstCalendarEvents.Location = new System.Drawing.Point(0, 26);
            this.lstCalendarEvents.Margin = new System.Windows.Forms.Padding(2);
            this.lstCalendarEvents.Name = "lstCalendarEvents";
            this.lstCalendarEvents.Size = new System.Drawing.Size(447, 380);
            this.lstCalendarEvents.TabIndex = 3;
            this.lstCalendarEvents.SelectedIndexChanged += new System.EventHandler(this.lstCalendarEvents_SelectedIndexChanged);
            // 
            // pnlEconomicCalendarEvents
            // 
            this.pnlEconomicCalendarEvents.BackColor = System.Drawing.Color.Gray;
            this.pnlEconomicCalendarEvents.Controls.Add(this.label2);
            this.pnlEconomicCalendarEvents.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlEconomicCalendarEvents.Location = new System.Drawing.Point(0, 0);
            this.pnlEconomicCalendarEvents.Name = "pnlEconomicCalendarEvents";
            this.pnlEconomicCalendarEvents.Size = new System.Drawing.Size(447, 26);
            this.pnlEconomicCalendarEvents.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(137, 4);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(177, 17);
            this.label2.TabIndex = 0;
            this.label2.Text = "Economic Calendar Events";
            // 
            // tlpCalendarEvents
            // 
            this.tlpCalendarEvents.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.tlpCalendarEvents.ColumnCount = 2;
            this.tlpCalendarEvents.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 28.14371F));
            this.tlpCalendarEvents.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 71.85629F));
            this.tlpCalendarEvents.Controls.Add(this.pnlContractId, 0, 0);
            this.tlpCalendarEvents.Controls.Add(this.pnlDescription, 0, 1);
            this.tlpCalendarEvents.Controls.Add(this.pnlContractMonth, 0, 2);
            this.tlpCalendarEvents.Controls.Add(this.pnlSymbol, 0, 3);
            this.tlpCalendarEvents.Controls.Add(this.pnlLocalSymbol, 0, 4);
            this.tlpCalendarEvents.Controls.Add(this.pnlSecurityType, 0, 6);
            this.tlpCalendarEvents.Controls.Add(this.dtmEventDate, 1, 0);
            this.tlpCalendarEvents.Controls.Add(this.txtEventName, 1, 2);
            this.tlpCalendarEvents.Controls.Add(this.pnlPrior, 0, 5);
            this.tlpCalendarEvents.Controls.Add(this.ddlCountryCodes, 1, 1);
            this.tlpCalendarEvents.Controls.Add(this.txtActual, 1, 3);
            this.tlpCalendarEvents.Controls.Add(this.txtForecast, 1, 4);
            this.tlpCalendarEvents.Controls.Add(this.txtPrior, 1, 5);
            this.tlpCalendarEvents.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpCalendarEvents.Location = new System.Drawing.Point(0, 0);
            this.tlpCalendarEvents.Margin = new System.Windows.Forms.Padding(2);
            this.tlpCalendarEvents.Name = "tlpCalendarEvents";
            this.tlpCalendarEvents.RowCount = 5;
            this.tlpCalendarEvents.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpCalendarEvents.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpCalendarEvents.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpCalendarEvents.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpCalendarEvents.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpCalendarEvents.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpCalendarEvents.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tlpCalendarEvents.Size = new System.Drawing.Size(434, 406);
            this.tlpCalendarEvents.TabIndex = 0;
            // 
            // pnlContractId
            // 
            this.pnlContractId.Controls.Add(this.lblEventDate);
            this.pnlContractId.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContractId.Location = new System.Drawing.Point(0, 0);
            this.pnlContractId.Margin = new System.Windows.Forms.Padding(0);
            this.pnlContractId.Name = "pnlContractId";
            this.pnlContractId.Size = new System.Drawing.Size(122, 30);
            this.pnlContractId.TabIndex = 0;
            // 
            // lblEventDate
            // 
            this.lblEventDate.AutoSize = true;
            this.lblEventDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblEventDate.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblEventDate.Location = new System.Drawing.Point(38, 3);
            this.lblEventDate.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblEventDate.Name = "lblEventDate";
            this.lblEventDate.Size = new System.Drawing.Size(82, 17);
            this.lblEventDate.TabIndex = 0;
            this.lblEventDate.Text = "Event Date:";
            this.lblEventDate.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // pnlDescription
            // 
            this.pnlDescription.Controls.Add(this.lblCurrency);
            this.pnlDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlDescription.Location = new System.Drawing.Point(2, 32);
            this.pnlDescription.Margin = new System.Windows.Forms.Padding(2);
            this.pnlDescription.Name = "pnlDescription";
            this.pnlDescription.Size = new System.Drawing.Size(118, 26);
            this.pnlDescription.TabIndex = 2;
            // 
            // lblCurrency
            // 
            this.lblCurrency.AutoSize = true;
            this.lblCurrency.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCurrency.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblCurrency.Location = new System.Drawing.Point(46, 3);
            this.lblCurrency.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblCurrency.Name = "lblCurrency";
            this.lblCurrency.Size = new System.Drawing.Size(69, 17);
            this.lblCurrency.TabIndex = 0;
            this.lblCurrency.Text = "Currency:";
            this.lblCurrency.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // pnlContractMonth
            // 
            this.pnlContractMonth.Controls.Add(this.lblEventName);
            this.pnlContractMonth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContractMonth.Location = new System.Drawing.Point(2, 62);
            this.pnlContractMonth.Margin = new System.Windows.Forms.Padding(2);
            this.pnlContractMonth.Name = "pnlContractMonth";
            this.pnlContractMonth.Size = new System.Drawing.Size(118, 26);
            this.pnlContractMonth.TabIndex = 4;
            // 
            // lblEventName
            // 
            this.lblEventName.AutoSize = true;
            this.lblEventName.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblEventName.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblEventName.Location = new System.Drawing.Point(29, 3);
            this.lblEventName.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblEventName.Name = "lblEventName";
            this.lblEventName.Size = new System.Drawing.Size(89, 17);
            this.lblEventName.TabIndex = 0;
            this.lblEventName.Text = "Event Name:";
            this.lblEventName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // pnlSymbol
            // 
            this.pnlSymbol.Controls.Add(this.lblActual);
            this.pnlSymbol.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSymbol.Location = new System.Drawing.Point(2, 92);
            this.pnlSymbol.Margin = new System.Windows.Forms.Padding(2);
            this.pnlSymbol.Name = "pnlSymbol";
            this.pnlSymbol.Size = new System.Drawing.Size(118, 26);
            this.pnlSymbol.TabIndex = 10;
            // 
            // lblActual
            // 
            this.lblActual.AutoSize = true;
            this.lblActual.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblActual.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblActual.Location = new System.Drawing.Point(67, 3);
            this.lblActual.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblActual.Name = "lblActual";
            this.lblActual.Size = new System.Drawing.Size(51, 17);
            this.lblActual.TabIndex = 0;
            this.lblActual.Text = "Actual:";
            this.lblActual.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // pnlLocalSymbol
            // 
            this.pnlLocalSymbol.Controls.Add(this.lblForecast);
            this.pnlLocalSymbol.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLocalSymbol.Location = new System.Drawing.Point(2, 122);
            this.pnlLocalSymbol.Margin = new System.Windows.Forms.Padding(2);
            this.pnlLocalSymbol.Name = "pnlLocalSymbol";
            this.pnlLocalSymbol.Size = new System.Drawing.Size(118, 26);
            this.pnlLocalSymbol.TabIndex = 12;
            // 
            // lblForecast
            // 
            this.lblForecast.AutoSize = true;
            this.lblForecast.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblForecast.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblForecast.Location = new System.Drawing.Point(52, 3);
            this.lblForecast.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblForecast.Name = "lblForecast";
            this.lblForecast.Size = new System.Drawing.Size(67, 17);
            this.lblForecast.TabIndex = 0;
            this.lblForecast.Text = "Forecast:";
            // 
            // pnlSecurityType
            // 
            this.pnlSecurityType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSecurityType.Location = new System.Drawing.Point(2, 182);
            this.pnlSecurityType.Margin = new System.Windows.Forms.Padding(2);
            this.pnlSecurityType.Name = "pnlSecurityType";
            this.pnlSecurityType.Size = new System.Drawing.Size(118, 222);
            this.pnlSecurityType.TabIndex = 14;
            // 
            // dtmEventDate
            // 
            this.dtmEventDate.CustomFormat = "yyyy-MM-dd hh:mm tt";
            this.dtmEventDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtmEventDate.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtmEventDate.Location = new System.Drawing.Point(125, 3);
            this.dtmEventDate.Name = "dtmEventDate";
            this.dtmEventDate.Size = new System.Drawing.Size(196, 23);
            this.dtmEventDate.TabIndex = 24;
            // 
            // txtEventName
            // 
            this.txtEventName.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtEventName.Location = new System.Drawing.Point(125, 63);
            this.txtEventName.Name = "txtEventName";
            this.txtEventName.Size = new System.Drawing.Size(306, 23);
            this.txtEventName.TabIndex = 26;
            // 
            // pnlPrior
            // 
            this.pnlPrior.Controls.Add(this.label1);
            this.pnlPrior.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlPrior.Location = new System.Drawing.Point(3, 153);
            this.pnlPrior.Name = "pnlPrior";
            this.pnlPrior.Size = new System.Drawing.Size(116, 24);
            this.pnlPrior.TabIndex = 27;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(76, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(38, 16);
            this.label1.TabIndex = 0;
            this.label1.Text = "Prior:";
            // 
            // ddlCountryCodes
            // 
            this.ddlCountryCodes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlCountryCodes.Enabled = false;
            this.ddlCountryCodes.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlCountryCodes.FormattingEnabled = true;
            this.ddlCountryCodes.Location = new System.Drawing.Point(124, 32);
            this.ddlCountryCodes.Margin = new System.Windows.Forms.Padding(2);
            this.ddlCountryCodes.Name = "ddlCountryCodes";
            this.ddlCountryCodes.Size = new System.Drawing.Size(197, 24);
            this.ddlCountryCodes.TabIndex = 25;
            this.ddlCountryCodes.SelectedIndexChanged += new System.EventHandler(this.ddlCountryCodes_SelectedIndexChanged);
            // 
            // txtActual
            // 
            this.txtActual.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtActual.Location = new System.Drawing.Point(125, 93);
            this.txtActual.Name = "txtActual";
            this.txtActual.Size = new System.Drawing.Size(196, 23);
            this.txtActual.TabIndex = 28;
            // 
            // txtForecast
            // 
            this.txtForecast.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtForecast.Location = new System.Drawing.Point(125, 123);
            this.txtForecast.Name = "txtForecast";
            this.txtForecast.Size = new System.Drawing.Size(197, 23);
            this.txtForecast.TabIndex = 29;
            // 
            // txtPrior
            // 
            this.txtPrior.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtPrior.Location = new System.Drawing.Point(125, 153);
            this.txtPrior.Name = "txtPrior";
            this.txtPrior.Size = new System.Drawing.Size(196, 23);
            this.txtPrior.TabIndex = 30;
            // 
            // lblLastTradeDate
            // 
            this.lblLastTradeDate.AutoSize = true;
            this.lblLastTradeDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLastTradeDate.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblLastTradeDate.Location = new System.Drawing.Point(11, 1);
            this.lblLastTradeDate.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblLastTradeDate.Name = "lblLastTradeDate";
            this.lblLastTradeDate.Size = new System.Drawing.Size(115, 17);
            this.lblLastTradeDate.TabIndex = 0;
            this.lblLastTradeDate.Text = "Last Trade Date:";
            this.lblLastTradeDate.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // EconomicCalendarEditorView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.Controls.Add(this.splitContainer1);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "EconomicCalendarEditorView";
            this.Size = new System.Drawing.Size(884, 406);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.pnlEconomicCalendarEvents.ResumeLayout(false);
            this.pnlEconomicCalendarEvents.PerformLayout();
            this.tlpCalendarEvents.ResumeLayout(false);
            this.tlpCalendarEvents.PerformLayout();
            this.pnlContractId.ResumeLayout(false);
            this.pnlContractId.PerformLayout();
            this.pnlDescription.ResumeLayout(false);
            this.pnlDescription.PerformLayout();
            this.pnlContractMonth.ResumeLayout(false);
            this.pnlContractMonth.PerformLayout();
            this.pnlSymbol.ResumeLayout(false);
            this.pnlSymbol.PerformLayout();
            this.pnlLocalSymbol.ResumeLayout(false);
            this.pnlLocalSymbol.PerformLayout();
            this.pnlPrior.ResumeLayout(false);
            this.pnlPrior.PerformLayout();
            this.ResumeLayout(false);

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
