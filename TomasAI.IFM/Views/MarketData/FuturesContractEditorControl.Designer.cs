namespace TomasAI.IFM.Views.MarketData
{
    partial class FuturesContractEditorControl
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
            this.lstFuturesContractIds = new System.Windows.Forms.ListBox();
            this.pnlFuturesContractIds = new System.Windows.Forms.Panel();
            this.lblFuturesContractIds = new System.Windows.Forms.Label();
            this.tlpFuturesOptionContract = new System.Windows.Forms.TableLayoutPanel();
            this.pnlContractId = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.txtContractId = new System.Windows.Forms.TextBox();
            this.pnlDescription = new System.Windows.Forms.Panel();
            this.lblDescription = new System.Windows.Forms.Label();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.pnlContractMonth = new System.Windows.Forms.Panel();
            this.lblLastTradeDate = new System.Windows.Forms.Label();
            this.dtmLastTradeDate = new System.Windows.Forms.DateTimePicker();
            this.pnlSymbol = new System.Windows.Forms.Panel();
            this.lblSymbol = new System.Windows.Forms.Label();
            this.ddlSymbol = new System.Windows.Forms.ComboBox();
            this.pnlLocalSymbol = new System.Windows.Forms.Panel();
            this.lblLocalSymbol = new System.Windows.Forms.Label();
            this.txtLocalSymbol = new System.Windows.Forms.TextBox();
            this.pnlSecurityType = new System.Windows.Forms.Panel();
            this.lblSecurityType = new System.Windows.Forms.Label();
            this.ddlSecurityType = new System.Windows.Forms.ComboBox();
            this.pnlCurrency = new System.Windows.Forms.Panel();
            this.lblCurrency = new System.Windows.Forms.Label();
            this.ddlCurrency = new System.Windows.Forms.ComboBox();
            this.pnlExchange = new System.Windows.Forms.Panel();
            this.lblExchange = new System.Windows.Forms.Label();
            this.ddlExchange = new System.Windows.Forms.ComboBox();
            this.pnlMultiplier = new System.Windows.Forms.Panel();
            this.lblMultiplier = new System.Windows.Forms.Label();
            this.ddlMultiplier = new System.Windows.Forms.ComboBox();
            this.ddlCurrentlyTraded = new System.Windows.Forms.ComboBox();
            this.pnlCurrentlyTraded = new System.Windows.Forms.Panel();
            this.lblCurrentlyTraded = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.pnlFuturesContractIds.SuspendLayout();
            this.tlpFuturesOptionContract.SuspendLayout();
            this.pnlContractId.SuspendLayout();
            this.pnlDescription.SuspendLayout();
            this.pnlContractMonth.SuspendLayout();
            this.pnlSymbol.SuspendLayout();
            this.pnlLocalSymbol.SuspendLayout();
            this.pnlSecurityType.SuspendLayout();
            this.pnlCurrency.SuspendLayout();
            this.pnlExchange.SuspendLayout();
            this.pnlMultiplier.SuspendLayout();
            this.pnlCurrentlyTraded.SuspendLayout();
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
            this.splitContainer1.Panel1.Controls.Add(this.lstFuturesContractIds);
            this.splitContainer1.Panel1.Controls.Add(this.pnlFuturesContractIds);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.tlpFuturesOptionContract);
            this.splitContainer1.Size = new System.Drawing.Size(943, 406);
            this.splitContainer1.SplitterDistance = 200;
            this.splitContainer1.SplitterWidth = 3;
            this.splitContainer1.TabIndex = 0;
            // 
            // lstFuturesContractIds
            // 
            this.lstFuturesContractIds.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lstFuturesContractIds.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lstFuturesContractIds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstFuturesContractIds.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstFuturesContractIds.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lstFuturesContractIds.FormattingEnabled = true;
            this.lstFuturesContractIds.ItemHeight = 16;
            this.lstFuturesContractIds.Location = new System.Drawing.Point(0, 26);
            this.lstFuturesContractIds.Margin = new System.Windows.Forms.Padding(2);
            this.lstFuturesContractIds.Name = "lstFuturesContractIds";
            this.lstFuturesContractIds.Size = new System.Drawing.Size(200, 380);
            this.lstFuturesContractIds.TabIndex = 5;
            this.lstFuturesContractIds.SelectedIndexChanged += new System.EventHandler(this.lstFuturesContractIds_SelectedIndexChanged);
            // 
            // pnlFuturesContractIds
            // 
            this.pnlFuturesContractIds.BackColor = System.Drawing.Color.Gray;
            this.pnlFuturesContractIds.Controls.Add(this.lblFuturesContractIds);
            this.pnlFuturesContractIds.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlFuturesContractIds.Location = new System.Drawing.Point(0, 0);
            this.pnlFuturesContractIds.Name = "pnlFuturesContractIds";
            this.pnlFuturesContractIds.Size = new System.Drawing.Size(200, 26);
            this.pnlFuturesContractIds.TabIndex = 3;
            // 
            // lblFuturesContractIds
            // 
            this.lblFuturesContractIds.AutoSize = true;
            this.lblFuturesContractIds.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFuturesContractIds.Location = new System.Drawing.Point(35, 4);
            this.lblFuturesContractIds.Name = "lblFuturesContractIds";
            this.lblFuturesContractIds.Size = new System.Drawing.Size(135, 17);
            this.lblFuturesContractIds.TabIndex = 0;
            this.lblFuturesContractIds.Text = "Futures Contract Ids";
            this.lblFuturesContractIds.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tlpFuturesOptionContract
            // 
            this.tlpFuturesOptionContract.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.tlpFuturesOptionContract.ColumnCount = 2;
            this.tlpFuturesOptionContract.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18.70748F));
            this.tlpFuturesOptionContract.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 81.29252F));
            this.tlpFuturesOptionContract.Controls.Add(this.pnlContractId, 0, 0);
            this.tlpFuturesOptionContract.Controls.Add(this.txtContractId, 1, 0);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlDescription, 0, 1);
            this.tlpFuturesOptionContract.Controls.Add(this.txtDescription, 1, 1);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlContractMonth, 0, 2);
            this.tlpFuturesOptionContract.Controls.Add(this.dtmLastTradeDate, 1, 2);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlSymbol, 0, 3);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlSymbol, 1, 3);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlLocalSymbol, 0, 4);
            this.tlpFuturesOptionContract.Controls.Add(this.txtLocalSymbol, 1, 4);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlSecurityType, 0, 5);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlSecurityType, 1, 5);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlCurrency, 0, 6);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlCurrency, 1, 6);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlExchange, 0, 7);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlExchange, 1, 7);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlMultiplier, 0, 8);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlMultiplier, 1, 8);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlCurrentlyTraded, 1, 9);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlCurrentlyTraded, 0, 9);
            this.tlpFuturesOptionContract.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpFuturesOptionContract.Location = new System.Drawing.Point(0, 0);
            this.tlpFuturesOptionContract.Margin = new System.Windows.Forms.Padding(2);
            this.tlpFuturesOptionContract.Name = "tlpFuturesOptionContract";
            this.tlpFuturesOptionContract.RowCount = 11;
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 51F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 31F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpFuturesOptionContract.Size = new System.Drawing.Size(740, 406);
            this.tlpFuturesOptionContract.TabIndex = 0;
            // 
            // pnlContractId
            // 
            this.pnlContractId.Controls.Add(this.label1);
            this.pnlContractId.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContractId.Location = new System.Drawing.Point(0, 0);
            this.pnlContractId.Margin = new System.Windows.Forms.Padding(0);
            this.pnlContractId.Name = "pnlContractId";
            this.pnlContractId.Size = new System.Drawing.Size(138, 28);
            this.pnlContractId.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.label1.Location = new System.Drawing.Point(46, 4);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "Contract Id:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtContractId
            // 
            this.txtContractId.BackColor = System.Drawing.SystemColors.Window;
            this.txtContractId.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtContractId.Dock = System.Windows.Forms.DockStyle.Left;
            this.txtContractId.Enabled = false;
            this.txtContractId.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtContractId.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.txtContractId.Location = new System.Drawing.Point(140, 2);
            this.txtContractId.Margin = new System.Windows.Forms.Padding(2);
            this.txtContractId.Name = "txtContractId";
            this.txtContractId.Size = new System.Drawing.Size(197, 23);
            this.txtContractId.TabIndex = 1;
            // 
            // pnlDescription
            // 
            this.pnlDescription.Controls.Add(this.lblDescription);
            this.pnlDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlDescription.Location = new System.Drawing.Point(2, 30);
            this.pnlDescription.Margin = new System.Windows.Forms.Padding(2);
            this.pnlDescription.Name = "pnlDescription";
            this.pnlDescription.Size = new System.Drawing.Size(134, 47);
            this.pnlDescription.TabIndex = 2;
            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDescription.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblDescription.Location = new System.Drawing.Point(40, 2);
            this.lblDescription.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(83, 17);
            this.lblDescription.TabIndex = 0;
            this.lblDescription.Text = "Description:";
            this.lblDescription.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtDescription
            // 
            this.txtDescription.BackColor = System.Drawing.SystemColors.Window;
            this.txtDescription.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDescription.Enabled = false;
            this.txtDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDescription.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.txtDescription.Location = new System.Drawing.Point(140, 30);
            this.txtDescription.Margin = new System.Windows.Forms.Padding(2);
            this.txtDescription.Multiline = true;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.Size = new System.Drawing.Size(598, 47);
            this.txtDescription.TabIndex = 3;
            // 
            // pnlContractMonth
            // 
            this.pnlContractMonth.Controls.Add(this.lblLastTradeDate);
            this.pnlContractMonth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContractMonth.Location = new System.Drawing.Point(2, 81);
            this.pnlContractMonth.Margin = new System.Windows.Forms.Padding(2);
            this.pnlContractMonth.Name = "pnlContractMonth";
            this.pnlContractMonth.Size = new System.Drawing.Size(134, 27);
            this.pnlContractMonth.TabIndex = 4;
            // 
            // lblLastTradeDate
            // 
            this.lblLastTradeDate.AutoSize = true;
            this.lblLastTradeDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLastTradeDate.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblLastTradeDate.Location = new System.Drawing.Point(5, 4);
            this.lblLastTradeDate.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblLastTradeDate.Name = "lblLastTradeDate";
            this.lblLastTradeDate.Size = new System.Drawing.Size(115, 17);
            this.lblLastTradeDate.TabIndex = 0;
            this.lblLastTradeDate.Text = "Last Trade Date:";
            this.lblLastTradeDate.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // dtmLastTradeDate
            // 
            this.dtmLastTradeDate.CalendarForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.dtmLastTradeDate.CalendarMonthBackground = System.Drawing.SystemColors.ControlDarkDark;
            this.dtmLastTradeDate.CalendarTitleBackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.dtmLastTradeDate.CalendarTitleForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.dtmLastTradeDate.CalendarTrailingForeColor = System.Drawing.SystemColors.ControlLight;
            this.dtmLastTradeDate.Dock = System.Windows.Forms.DockStyle.Left;
            this.dtmLastTradeDate.Enabled = false;
            this.dtmLastTradeDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtmLastTradeDate.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtmLastTradeDate.Location = new System.Drawing.Point(140, 81);
            this.dtmLastTradeDate.Margin = new System.Windows.Forms.Padding(2);
            this.dtmLastTradeDate.Name = "dtmLastTradeDate";
            this.dtmLastTradeDate.Size = new System.Drawing.Size(197, 23);
            this.dtmLastTradeDate.TabIndex = 5;
            this.dtmLastTradeDate.ValueChanged += new System.EventHandler(this.dtmContractMonth_ValueChanged);
            // 
            // pnlSymbol
            // 
            this.pnlSymbol.Controls.Add(this.lblSymbol);
            this.pnlSymbol.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSymbol.Location = new System.Drawing.Point(2, 112);
            this.pnlSymbol.Margin = new System.Windows.Forms.Padding(2);
            this.pnlSymbol.Name = "pnlSymbol";
            this.pnlSymbol.Size = new System.Drawing.Size(134, 21);
            this.pnlSymbol.TabIndex = 10;
            // 
            // lblSymbol
            // 
            this.lblSymbol.AutoSize = true;
            this.lblSymbol.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSymbol.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblSymbol.Location = new System.Drawing.Point(60, 2);
            this.lblSymbol.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblSymbol.Name = "lblSymbol";
            this.lblSymbol.Size = new System.Drawing.Size(58, 17);
            this.lblSymbol.TabIndex = 0;
            this.lblSymbol.Text = "Symbol:";
            this.lblSymbol.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // ddlSymbol
            // 
            this.ddlSymbol.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.ddlSymbol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlSymbol.Enabled = false;
            this.ddlSymbol.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlSymbol.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.ddlSymbol.FormattingEnabled = true;
            this.ddlSymbol.Location = new System.Drawing.Point(140, 112);
            this.ddlSymbol.Margin = new System.Windows.Forms.Padding(2);
            this.ddlSymbol.Name = "ddlSymbol";
            this.ddlSymbol.Size = new System.Drawing.Size(197, 24);
            this.ddlSymbol.TabIndex = 11;
            this.ddlSymbol.SelectedIndexChanged += new System.EventHandler(this.ddlSymbol_SelectedIndexChanged);
            // 
            // pnlLocalSymbol
            // 
            this.pnlLocalSymbol.Controls.Add(this.lblLocalSymbol);
            this.pnlLocalSymbol.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLocalSymbol.Location = new System.Drawing.Point(2, 137);
            this.pnlLocalSymbol.Margin = new System.Windows.Forms.Padding(2);
            this.pnlLocalSymbol.Name = "pnlLocalSymbol";
            this.pnlLocalSymbol.Size = new System.Drawing.Size(134, 24);
            this.pnlLocalSymbol.TabIndex = 12;
            // 
            // lblLocalSymbol
            // 
            this.lblLocalSymbol.AutoSize = true;
            this.lblLocalSymbol.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLocalSymbol.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblLocalSymbol.Location = new System.Drawing.Point(24, 2);
            this.lblLocalSymbol.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblLocalSymbol.Name = "lblLocalSymbol";
            this.lblLocalSymbol.Size = new System.Drawing.Size(96, 17);
            this.lblLocalSymbol.TabIndex = 0;
            this.lblLocalSymbol.Text = "Local Symbol:";
            // 
            // txtLocalSymbol
            // 
            this.txtLocalSymbol.Enabled = false;
            this.txtLocalSymbol.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtLocalSymbol.Location = new System.Drawing.Point(140, 137);
            this.txtLocalSymbol.Margin = new System.Windows.Forms.Padding(2);
            this.txtLocalSymbol.Name = "txtLocalSymbol";
            this.txtLocalSymbol.Size = new System.Drawing.Size(110, 23);
            this.txtLocalSymbol.TabIndex = 13;
            // 
            // pnlSecurityType
            // 
            this.pnlSecurityType.Controls.Add(this.lblSecurityType);
            this.pnlSecurityType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSecurityType.Location = new System.Drawing.Point(2, 165);
            this.pnlSecurityType.Margin = new System.Windows.Forms.Padding(2);
            this.pnlSecurityType.Name = "pnlSecurityType";
            this.pnlSecurityType.Size = new System.Drawing.Size(134, 24);
            this.pnlSecurityType.TabIndex = 14;
            // 
            // lblSecurityType
            // 
            this.lblSecurityType.AutoSize = true;
            this.lblSecurityType.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSecurityType.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblSecurityType.Location = new System.Drawing.Point(20, 2);
            this.lblSecurityType.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblSecurityType.Name = "lblSecurityType";
            this.lblSecurityType.Size = new System.Drawing.Size(99, 17);
            this.lblSecurityType.TabIndex = 0;
            this.lblSecurityType.Text = "Security Type:";
            this.lblSecurityType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // ddlSecurityType
            // 
            this.ddlSecurityType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlSecurityType.Enabled = false;
            this.ddlSecurityType.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlSecurityType.FormattingEnabled = true;
            this.ddlSecurityType.Location = new System.Drawing.Point(140, 165);
            this.ddlSecurityType.Margin = new System.Windows.Forms.Padding(2);
            this.ddlSecurityType.Name = "ddlSecurityType";
            this.ddlSecurityType.Size = new System.Drawing.Size(197, 24);
            this.ddlSecurityType.TabIndex = 15;
            // 
            // pnlCurrency
            // 
            this.pnlCurrency.Controls.Add(this.lblCurrency);
            this.pnlCurrency.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlCurrency.Location = new System.Drawing.Point(2, 193);
            this.pnlCurrency.Margin = new System.Windows.Forms.Padding(2);
            this.pnlCurrency.Name = "pnlCurrency";
            this.pnlCurrency.Size = new System.Drawing.Size(134, 26);
            this.pnlCurrency.TabIndex = 16;
            // 
            // lblCurrency
            // 
            this.lblCurrency.AutoSize = true;
            this.lblCurrency.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCurrency.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblCurrency.Location = new System.Drawing.Point(50, 2);
            this.lblCurrency.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblCurrency.Name = "lblCurrency";
            this.lblCurrency.Size = new System.Drawing.Size(69, 17);
            this.lblCurrency.TabIndex = 0;
            this.lblCurrency.Text = "Currency:";
            this.lblCurrency.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // ddlCurrency
            // 
            this.ddlCurrency.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlCurrency.Enabled = false;
            this.ddlCurrency.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlCurrency.FormattingEnabled = true;
            this.ddlCurrency.Location = new System.Drawing.Point(140, 193);
            this.ddlCurrency.Margin = new System.Windows.Forms.Padding(2);
            this.ddlCurrency.Name = "ddlCurrency";
            this.ddlCurrency.Size = new System.Drawing.Size(197, 24);
            this.ddlCurrency.TabIndex = 17;
            // 
            // pnlExchange
            // 
            this.pnlExchange.Controls.Add(this.lblExchange);
            this.pnlExchange.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlExchange.Location = new System.Drawing.Point(2, 223);
            this.pnlExchange.Margin = new System.Windows.Forms.Padding(2);
            this.pnlExchange.Name = "pnlExchange";
            this.pnlExchange.Size = new System.Drawing.Size(134, 22);
            this.pnlExchange.TabIndex = 18;
            // 
            // lblExchange
            // 
            this.lblExchange.AutoSize = true;
            this.lblExchange.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblExchange.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblExchange.Location = new System.Drawing.Point(46, 2);
            this.lblExchange.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblExchange.Name = "lblExchange";
            this.lblExchange.Size = new System.Drawing.Size(74, 17);
            this.lblExchange.TabIndex = 0;
            this.lblExchange.Text = "Exchange:";
            this.lblExchange.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // ddlExchange
            // 
            this.ddlExchange.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlExchange.Enabled = false;
            this.ddlExchange.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlExchange.FormattingEnabled = true;
            this.ddlExchange.Location = new System.Drawing.Point(140, 223);
            this.ddlExchange.Margin = new System.Windows.Forms.Padding(2);
            this.ddlExchange.Name = "ddlExchange";
            this.ddlExchange.Size = new System.Drawing.Size(197, 24);
            this.ddlExchange.TabIndex = 19;
            this.ddlExchange.SelectedIndexChanged += new System.EventHandler(this.ddlExchange_SelectedIndexChanged);
            // 
            // pnlMultiplier
            // 
            this.pnlMultiplier.Controls.Add(this.lblMultiplier);
            this.pnlMultiplier.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMultiplier.Location = new System.Drawing.Point(2, 249);
            this.pnlMultiplier.Margin = new System.Windows.Forms.Padding(2);
            this.pnlMultiplier.Name = "pnlMultiplier";
            this.pnlMultiplier.Size = new System.Drawing.Size(134, 24);
            this.pnlMultiplier.TabIndex = 20;
            // 
            // lblMultiplier
            // 
            this.lblMultiplier.AutoSize = true;
            this.lblMultiplier.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMultiplier.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblMultiplier.Location = new System.Drawing.Point(51, 2);
            this.lblMultiplier.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblMultiplier.Name = "lblMultiplier";
            this.lblMultiplier.Size = new System.Drawing.Size(68, 17);
            this.lblMultiplier.TabIndex = 0;
            this.lblMultiplier.Text = "Multiplier:";
            this.lblMultiplier.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // ddlMultiplier
            // 
            this.ddlMultiplier.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlMultiplier.Enabled = false;
            this.ddlMultiplier.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlMultiplier.FormattingEnabled = true;
            this.ddlMultiplier.Location = new System.Drawing.Point(140, 249);
            this.ddlMultiplier.Margin = new System.Windows.Forms.Padding(2);
            this.ddlMultiplier.Name = "ddlMultiplier";
            this.ddlMultiplier.Size = new System.Drawing.Size(197, 24);
            this.ddlMultiplier.TabIndex = 21;
            // 
            // ddlCurrentlyTraded
            // 
            this.ddlCurrentlyTraded.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlCurrentlyTraded.Enabled = false;
            this.ddlCurrentlyTraded.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlCurrentlyTraded.FormattingEnabled = true;
            this.ddlCurrentlyTraded.Location = new System.Drawing.Point(141, 278);
            this.ddlCurrentlyTraded.Name = "ddlCurrentlyTraded";
            this.ddlCurrentlyTraded.Size = new System.Drawing.Size(196, 24);
            this.ddlCurrentlyTraded.TabIndex = 22;
            // 
            // pnlCurrentlyTraded
            // 
            this.pnlCurrentlyTraded.Controls.Add(this.lblCurrentlyTraded);
            this.pnlCurrentlyTraded.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlCurrentlyTraded.Location = new System.Drawing.Point(2, 277);
            this.pnlCurrentlyTraded.Margin = new System.Windows.Forms.Padding(2);
            this.pnlCurrentlyTraded.Name = "pnlCurrentlyTraded";
            this.pnlCurrentlyTraded.Size = new System.Drawing.Size(134, 24);
            this.pnlCurrentlyTraded.TabIndex = 23;
            // 
            // lblCurrentlyTraded
            // 
            this.lblCurrentlyTraded.AutoSize = true;
            this.lblCurrentlyTraded.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCurrentlyTraded.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblCurrentlyTraded.Location = new System.Drawing.Point(8, 4);
            this.lblCurrentlyTraded.Name = "lblCurrentlyTraded";
            this.lblCurrentlyTraded.Size = new System.Drawing.Size(110, 16);
            this.lblCurrentlyTraded.TabIndex = 0;
            this.lblCurrentlyTraded.Text = "Currently Traded:";
            // 
            // FuturesContractEditorControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.Controls.Add(this.splitContainer1);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "FuturesContractEditorControl";
            this.Size = new System.Drawing.Size(943, 406);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.pnlFuturesContractIds.ResumeLayout(false);
            this.pnlFuturesContractIds.PerformLayout();
            this.tlpFuturesOptionContract.ResumeLayout(false);
            this.tlpFuturesOptionContract.PerformLayout();
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
            this.pnlSecurityType.ResumeLayout(false);
            this.pnlSecurityType.PerformLayout();
            this.pnlCurrency.ResumeLayout(false);
            this.pnlCurrency.PerformLayout();
            this.pnlExchange.ResumeLayout(false);
            this.pnlExchange.PerformLayout();
            this.pnlMultiplier.ResumeLayout(false);
            this.pnlMultiplier.PerformLayout();
            this.pnlCurrentlyTraded.ResumeLayout(false);
            this.pnlCurrentlyTraded.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TableLayoutPanel tlpFuturesOptionContract;
        private System.Windows.Forms.Panel pnlContractId;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtContractId;
        private System.Windows.Forms.Panel pnlDescription;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.Panel pnlContractMonth;
        private System.Windows.Forms.Label lblLastTradeDate;
        private System.Windows.Forms.DateTimePicker dtmLastTradeDate;
        private System.Windows.Forms.Panel pnlSymbol;
        private System.Windows.Forms.Label lblSymbol;
        private System.Windows.Forms.ComboBox ddlSymbol;
        private System.Windows.Forms.Panel pnlLocalSymbol;
        private System.Windows.Forms.Label lblLocalSymbol;
        private System.Windows.Forms.TextBox txtLocalSymbol;
        private System.Windows.Forms.Panel pnlSecurityType;
        private System.Windows.Forms.Label lblSecurityType;
        private System.Windows.Forms.ComboBox ddlSecurityType;
        private System.Windows.Forms.Panel pnlCurrency;
        private System.Windows.Forms.Label lblCurrency;
        private System.Windows.Forms.ComboBox ddlCurrency;
        private System.Windows.Forms.Panel pnlExchange;
        private System.Windows.Forms.Label lblExchange;
        private System.Windows.Forms.ComboBox ddlExchange;
        private System.Windows.Forms.Panel pnlMultiplier;
        private System.Windows.Forms.Label lblMultiplier;
        private System.Windows.Forms.ComboBox ddlMultiplier;
        private System.Windows.Forms.ComboBox ddlCurrentlyTraded;
        private System.Windows.Forms.Panel pnlCurrentlyTraded;
        private System.Windows.Forms.Label lblCurrentlyTraded;
        private System.Windows.Forms.Panel pnlFuturesContractIds;
        private System.Windows.Forms.ListBox lstFuturesContractIds;
        private System.Windows.Forms.Label lblFuturesContractIds;
    }
}
