namespace TomasAI.IFM.UI.Net.Views.MarketData
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
            splitContainer1 = new SplitContainer();
            lstFuturesContractIds = new ListBox();
            pnlFuturesContractIds = new Panel();
            lblFuturesContractIds = new Label();
            tlpFuturesOptionContract = new TableLayoutPanel();
            pnlContractId = new Panel();
            label1 = new Label();
            txtContractId = new TextBox();
            pnlDescription = new Panel();
            lblDescription = new Label();
            txtDescription = new TextBox();
            pnlContractMonth = new Panel();
            lblLastTradeDate = new Label();
            dtmLastTradeDate = new DateTimePicker();
            pnlSymbol = new Panel();
            lblSymbol = new Label();
            ddlSymbol = new ComboBox();
            pnlLocalSymbol = new Panel();
            lblLocalSymbol = new Label();
            txtLocalSymbol = new TextBox();
            pnlSecurityType = new Panel();
            lblSecurityType = new Label();
            ddlSecurityType = new ComboBox();
            pnlCurrency = new Panel();
            lblCurrency = new Label();
            ddlCurrency = new ComboBox();
            pnlExchange = new Panel();
            lblExchange = new Label();
            ddlExchange = new ComboBox();
            pnlMultiplier = new Panel();
            lblMultiplier = new Label();
            ddlMultiplier = new ComboBox();
            ddlCurrentlyTraded = new ComboBox();
            pnlCurrentlyTraded = new Panel();
            lblCurrentlyTraded = new Label();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            pnlFuturesContractIds.SuspendLayout();
            tlpFuturesOptionContract.SuspendLayout();
            pnlContractId.SuspendLayout();
            pnlDescription.SuspendLayout();
            pnlContractMonth.SuspendLayout();
            pnlSymbol.SuspendLayout();
            pnlLocalSymbol.SuspendLayout();
            pnlSecurityType.SuspendLayout();
            pnlCurrency.SuspendLayout();
            pnlExchange.SuspendLayout();
            pnlMultiplier.SuspendLayout();
            pnlCurrentlyTraded.SuspendLayout();
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
            splitContainer1.Panel1.Controls.Add(lstFuturesContractIds);
            splitContainer1.Panel1.Controls.Add(pnlFuturesContractIds);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(tlpFuturesOptionContract);
            splitContainer1.Size = new Size(1100, 468);
            splitContainer1.SplitterDistance = 232;
            splitContainer1.TabIndex = 0;
            // 
            // lstFuturesContractIds
            // 
            lstFuturesContractIds.BackColor = Color.FromArgb(64, 64, 64);
            lstFuturesContractIds.BorderStyle = BorderStyle.FixedSingle;
            lstFuturesContractIds.Dock = DockStyle.Fill;
            lstFuturesContractIds.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lstFuturesContractIds.ForeColor = Color.White;
            lstFuturesContractIds.FormattingEnabled = true;
            lstFuturesContractIds.Location = new Point(0, 30);
            lstFuturesContractIds.Margin = new Padding(2);
            lstFuturesContractIds.Name = "lstFuturesContractIds";
            lstFuturesContractIds.Size = new Size(232, 438);
            lstFuturesContractIds.TabIndex = 5;
            lstFuturesContractIds.SelectedIndexChanged += lstFuturesContractIds_SelectedIndexChanged;
            // 
            // pnlFuturesContractIds
            // 
            pnlFuturesContractIds.BackColor = Color.Gray;
            pnlFuturesContractIds.Controls.Add(lblFuturesContractIds);
            pnlFuturesContractIds.Dock = DockStyle.Top;
            pnlFuturesContractIds.Location = new Point(0, 0);
            pnlFuturesContractIds.Margin = new Padding(4, 3, 4, 3);
            pnlFuturesContractIds.Name = "pnlFuturesContractIds";
            pnlFuturesContractIds.Size = new Size(232, 30);
            pnlFuturesContractIds.TabIndex = 3;
            // 
            // lblFuturesContractIds
            // 
            lblFuturesContractIds.AutoSize = true;
            lblFuturesContractIds.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblFuturesContractIds.ForeColor = Color.Black;
            lblFuturesContractIds.Location = new Point(41, 5);
            lblFuturesContractIds.Margin = new Padding(4, 0, 4, 0);
            lblFuturesContractIds.Name = "lblFuturesContractIds";
            lblFuturesContractIds.Size = new Size(135, 17);
            lblFuturesContractIds.TabIndex = 0;
            lblFuturesContractIds.Text = "Futures Contract Ids";
            lblFuturesContractIds.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // tlpFuturesOptionContract
            // 
            tlpFuturesOptionContract.BackColor = Color.FromArgb(64, 64, 64);
            tlpFuturesOptionContract.ColumnCount = 2;
            tlpFuturesOptionContract.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18.70748F));
            tlpFuturesOptionContract.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 81.29252F));
            tlpFuturesOptionContract.Controls.Add(pnlContractId, 0, 0);
            tlpFuturesOptionContract.Controls.Add(txtContractId, 1, 0);
            tlpFuturesOptionContract.Controls.Add(pnlDescription, 0, 1);
            tlpFuturesOptionContract.Controls.Add(txtDescription, 1, 1);
            tlpFuturesOptionContract.Controls.Add(pnlContractMonth, 0, 2);
            tlpFuturesOptionContract.Controls.Add(dtmLastTradeDate, 1, 2);
            tlpFuturesOptionContract.Controls.Add(pnlSymbol, 0, 3);
            tlpFuturesOptionContract.Controls.Add(ddlSymbol, 1, 3);
            tlpFuturesOptionContract.Controls.Add(pnlLocalSymbol, 0, 4);
            tlpFuturesOptionContract.Controls.Add(txtLocalSymbol, 1, 4);
            tlpFuturesOptionContract.Controls.Add(pnlSecurityType, 0, 5);
            tlpFuturesOptionContract.Controls.Add(ddlSecurityType, 1, 5);
            tlpFuturesOptionContract.Controls.Add(pnlCurrency, 0, 6);
            tlpFuturesOptionContract.Controls.Add(ddlCurrency, 1, 6);
            tlpFuturesOptionContract.Controls.Add(pnlExchange, 0, 7);
            tlpFuturesOptionContract.Controls.Add(ddlExchange, 1, 7);
            tlpFuturesOptionContract.Controls.Add(pnlMultiplier, 0, 8);
            tlpFuturesOptionContract.Controls.Add(ddlMultiplier, 1, 8);
            tlpFuturesOptionContract.Controls.Add(ddlCurrentlyTraded, 1, 9);
            tlpFuturesOptionContract.Controls.Add(pnlCurrentlyTraded, 0, 9);
            tlpFuturesOptionContract.Dock = DockStyle.Fill;
            tlpFuturesOptionContract.Location = new Point(0, 0);
            tlpFuturesOptionContract.Margin = new Padding(2);
            tlpFuturesOptionContract.Name = "tlpFuturesOptionContract";
            tlpFuturesOptionContract.RowCount = 11;
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 29F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpFuturesOptionContract.Size = new Size(864, 468);
            tlpFuturesOptionContract.TabIndex = 0;
            // 
            // pnlContractId
            // 
            pnlContractId.Controls.Add(label1);
            pnlContractId.Dock = DockStyle.Fill;
            pnlContractId.Location = new Point(0, 0);
            pnlContractId.Margin = new Padding(0);
            pnlContractId.Name = "pnlContractId";
            pnlContractId.Size = new Size(161, 32);
            pnlContractId.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label1.ForeColor = Color.White;
            label1.Location = new Point(54, 5);
            label1.Margin = new Padding(2, 0, 2, 0);
            label1.Name = "label1";
            label1.Size = new Size(80, 17);
            label1.TabIndex = 0;
            label1.Text = "Contract Id:";
            label1.TextAlign = ContentAlignment.MiddleRight;
            // 
            // txtContractId
            // 
            txtContractId.BackColor = Color.Black;
            txtContractId.BorderStyle = BorderStyle.FixedSingle;
            txtContractId.Dock = DockStyle.Left;
            txtContractId.Enabled = false;
            txtContractId.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtContractId.ForeColor = SystemColors.GrayText;
            txtContractId.Location = new Point(163, 2);
            txtContractId.Margin = new Padding(2);
            txtContractId.Name = "txtContractId";
            txtContractId.Size = new Size(229, 23);
            txtContractId.TabIndex = 1;
            txtContractId.Text = "1234";
            // 
            // pnlDescription
            // 
            pnlDescription.Controls.Add(lblDescription);
            pnlDescription.Dock = DockStyle.Fill;
            pnlDescription.Location = new Point(2, 34);
            pnlDescription.Margin = new Padding(2);
            pnlDescription.Name = "pnlDescription";
            pnlDescription.Size = new Size(157, 55);
            pnlDescription.TabIndex = 2;
            // 
            // lblDescription
            // 
            lblDescription.AutoSize = true;
            lblDescription.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblDescription.ForeColor = Color.White;
            lblDescription.Location = new Point(47, 2);
            lblDescription.Margin = new Padding(2, 0, 2, 0);
            lblDescription.Name = "lblDescription";
            lblDescription.Size = new Size(83, 17);
            lblDescription.TabIndex = 0;
            lblDescription.Text = "Description:";
            lblDescription.TextAlign = ContentAlignment.MiddleRight;
            // 
            // txtDescription
            // 
            txtDescription.BackColor = Color.Black;
            txtDescription.BorderStyle = BorderStyle.FixedSingle;
            txtDescription.Dock = DockStyle.Fill;
            txtDescription.Enabled = false;
            txtDescription.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtDescription.ForeColor = SystemColors.GrayText;
            txtDescription.Location = new Point(163, 34);
            txtDescription.Margin = new Padding(2);
            txtDescription.Multiline = true;
            txtDescription.Name = "txtDescription";
            txtDescription.Size = new Size(699, 55);
            txtDescription.TabIndex = 3;
            txtDescription.Text = "5678";
            // 
            // pnlContractMonth
            // 
            pnlContractMonth.Controls.Add(lblLastTradeDate);
            pnlContractMonth.Dock = DockStyle.Fill;
            pnlContractMonth.Location = new Point(2, 93);
            pnlContractMonth.Margin = new Padding(2);
            pnlContractMonth.Name = "pnlContractMonth";
            pnlContractMonth.Size = new Size(157, 32);
            pnlContractMonth.TabIndex = 4;
            // 
            // lblLastTradeDate
            // 
            lblLastTradeDate.AutoSize = true;
            lblLastTradeDate.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblLastTradeDate.ForeColor = Color.White;
            lblLastTradeDate.Location = new Point(9, 6);
            lblLastTradeDate.Margin = new Padding(2, 0, 2, 0);
            lblLastTradeDate.Name = "lblLastTradeDate";
            lblLastTradeDate.Size = new Size(115, 17);
            lblLastTradeDate.TabIndex = 0;
            lblLastTradeDate.Text = "Last Trade Date:";
            lblLastTradeDate.TextAlign = ContentAlignment.MiddleRight;
            // 
            // dtmLastTradeDate
            // 
            dtmLastTradeDate.CalendarForeColor = SystemColors.ControlLightLight;
            dtmLastTradeDate.CalendarMonthBackground = SystemColors.ControlDarkDark;
            dtmLastTradeDate.CalendarTitleBackColor = SystemColors.ControlDarkDark;
            dtmLastTradeDate.CalendarTitleForeColor = SystemColors.ControlLightLight;
            dtmLastTradeDate.CalendarTrailingForeColor = SystemColors.ControlLight;
            dtmLastTradeDate.Dock = DockStyle.Left;
            dtmLastTradeDate.Enabled = false;
            dtmLastTradeDate.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dtmLastTradeDate.Format = DateTimePickerFormat.Short;
            dtmLastTradeDate.Location = new Point(163, 93);
            dtmLastTradeDate.Margin = new Padding(2);
            dtmLastTradeDate.Name = "dtmLastTradeDate";
            dtmLastTradeDate.Size = new Size(229, 23);
            dtmLastTradeDate.TabIndex = 5;
            dtmLastTradeDate.ValueChanged += dtmContractMonth_ValueChanged;
            // 
            // pnlSymbol
            // 
            pnlSymbol.Controls.Add(lblSymbol);
            pnlSymbol.Dock = DockStyle.Fill;
            pnlSymbol.Location = new Point(2, 129);
            pnlSymbol.Margin = new Padding(2);
            pnlSymbol.Name = "pnlSymbol";
            pnlSymbol.Size = new Size(157, 25);
            pnlSymbol.TabIndex = 10;
            // 
            // lblSymbol
            // 
            lblSymbol.AutoSize = true;
            lblSymbol.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblSymbol.ForeColor = Color.White;
            lblSymbol.Location = new Point(70, 2);
            lblSymbol.Margin = new Padding(2, 0, 2, 0);
            lblSymbol.Name = "lblSymbol";
            lblSymbol.Size = new Size(58, 17);
            lblSymbol.TabIndex = 0;
            lblSymbol.Text = "Symbol:";
            lblSymbol.TextAlign = ContentAlignment.MiddleRight;
            // 
            // ddlSymbol
            // 
            ddlSymbol.BackColor = Color.Black;
            ddlSymbol.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlSymbol.Enabled = false;
            ddlSymbol.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlSymbol.ForeColor = Color.White;
            ddlSymbol.FormattingEnabled = true;
            ddlSymbol.Location = new Point(163, 129);
            ddlSymbol.Margin = new Padding(2);
            ddlSymbol.Name = "ddlSymbol";
            ddlSymbol.Size = new Size(229, 24);
            ddlSymbol.TabIndex = 11;
            ddlSymbol.SelectedIndexChanged += ddlSymbol_SelectedIndexChanged;
            // 
            // pnlLocalSymbol
            // 
            pnlLocalSymbol.Controls.Add(lblLocalSymbol);
            pnlLocalSymbol.Dock = DockStyle.Fill;
            pnlLocalSymbol.Location = new Point(2, 158);
            pnlLocalSymbol.Margin = new Padding(2);
            pnlLocalSymbol.Name = "pnlLocalSymbol";
            pnlLocalSymbol.Size = new Size(157, 28);
            pnlLocalSymbol.TabIndex = 12;
            // 
            // lblLocalSymbol
            // 
            lblLocalSymbol.AutoSize = true;
            lblLocalSymbol.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblLocalSymbol.ForeColor = Color.White;
            lblLocalSymbol.Location = new Point(28, 6);
            lblLocalSymbol.Margin = new Padding(2, 0, 2, 0);
            lblLocalSymbol.Name = "lblLocalSymbol";
            lblLocalSymbol.Size = new Size(96, 17);
            lblLocalSymbol.TabIndex = 0;
            lblLocalSymbol.Text = "Local Symbol:";
            // 
            // txtLocalSymbol
            // 
            txtLocalSymbol.BackColor = Color.Black;
            txtLocalSymbol.BorderStyle = BorderStyle.FixedSingle;
            txtLocalSymbol.Enabled = false;
            txtLocalSymbol.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtLocalSymbol.ForeColor = SystemColors.GrayText;
            txtLocalSymbol.Location = new Point(163, 158);
            txtLocalSymbol.Margin = new Padding(2);
            txtLocalSymbol.Name = "txtLocalSymbol";
            txtLocalSymbol.Size = new Size(128, 23);
            txtLocalSymbol.TabIndex = 13;
            txtLocalSymbol.Text = "2468";
            // 
            // pnlSecurityType
            // 
            pnlSecurityType.Controls.Add(lblSecurityType);
            pnlSecurityType.Dock = DockStyle.Fill;
            pnlSecurityType.Location = new Point(2, 190);
            pnlSecurityType.Margin = new Padding(2);
            pnlSecurityType.Name = "pnlSecurityType";
            pnlSecurityType.Size = new Size(157, 28);
            pnlSecurityType.TabIndex = 14;
            // 
            // lblSecurityType
            // 
            lblSecurityType.AutoSize = true;
            lblSecurityType.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblSecurityType.ForeColor = Color.White;
            lblSecurityType.Location = new Point(25, 7);
            lblSecurityType.Margin = new Padding(2, 0, 2, 0);
            lblSecurityType.Name = "lblSecurityType";
            lblSecurityType.Size = new Size(99, 17);
            lblSecurityType.TabIndex = 0;
            lblSecurityType.Text = "Security Type:";
            lblSecurityType.TextAlign = ContentAlignment.MiddleRight;
            // 
            // ddlSecurityType
            // 
            ddlSecurityType.BackColor = Color.Black;
            ddlSecurityType.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlSecurityType.Enabled = false;
            ddlSecurityType.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlSecurityType.ForeColor = Color.White;
            ddlSecurityType.FormattingEnabled = true;
            ddlSecurityType.Location = new Point(163, 190);
            ddlSecurityType.Margin = new Padding(2);
            ddlSecurityType.Name = "ddlSecurityType";
            ddlSecurityType.Size = new Size(229, 24);
            ddlSecurityType.TabIndex = 15;
            // 
            // pnlCurrency
            // 
            pnlCurrency.Controls.Add(lblCurrency);
            pnlCurrency.Dock = DockStyle.Fill;
            pnlCurrency.Location = new Point(2, 222);
            pnlCurrency.Margin = new Padding(2);
            pnlCurrency.Name = "pnlCurrency";
            pnlCurrency.Size = new Size(157, 31);
            pnlCurrency.TabIndex = 16;
            // 
            // lblCurrency
            // 
            lblCurrency.AutoSize = true;
            lblCurrency.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblCurrency.ForeColor = Color.White;
            lblCurrency.Location = new Point(55, 7);
            lblCurrency.Margin = new Padding(2, 0, 2, 0);
            lblCurrency.Name = "lblCurrency";
            lblCurrency.Size = new Size(69, 17);
            lblCurrency.TabIndex = 0;
            lblCurrency.Text = "Currency:";
            lblCurrency.TextAlign = ContentAlignment.MiddleRight;
            // 
            // ddlCurrency
            // 
            ddlCurrency.BackColor = Color.Black;
            ddlCurrency.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlCurrency.Enabled = false;
            ddlCurrency.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlCurrency.ForeColor = Color.White;
            ddlCurrency.FormattingEnabled = true;
            ddlCurrency.Location = new Point(163, 222);
            ddlCurrency.Margin = new Padding(2);
            ddlCurrency.Name = "ddlCurrency";
            ddlCurrency.Size = new Size(229, 24);
            ddlCurrency.TabIndex = 17;
            // 
            // pnlExchange
            // 
            pnlExchange.Controls.Add(lblExchange);
            pnlExchange.Dock = DockStyle.Fill;
            pnlExchange.Location = new Point(2, 257);
            pnlExchange.Margin = new Padding(2);
            pnlExchange.Name = "pnlExchange";
            pnlExchange.Size = new Size(157, 26);
            pnlExchange.TabIndex = 18;
            // 
            // lblExchange
            // 
            lblExchange.AutoSize = true;
            lblExchange.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblExchange.ForeColor = Color.White;
            lblExchange.Location = new Point(50, 2);
            lblExchange.Margin = new Padding(2, 0, 2, 0);
            lblExchange.Name = "lblExchange";
            lblExchange.Size = new Size(74, 17);
            lblExchange.TabIndex = 0;
            lblExchange.Text = "Exchange:";
            lblExchange.TextAlign = ContentAlignment.MiddleRight;
            // 
            // ddlExchange
            // 
            ddlExchange.BackColor = Color.Black;
            ddlExchange.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlExchange.Enabled = false;
            ddlExchange.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlExchange.ForeColor = Color.White;
            ddlExchange.FormattingEnabled = true;
            ddlExchange.Location = new Point(163, 257);
            ddlExchange.Margin = new Padding(2);
            ddlExchange.Name = "ddlExchange";
            ddlExchange.Size = new Size(229, 24);
            ddlExchange.TabIndex = 19;
            ddlExchange.SelectedIndexChanged += ddlExchange_SelectedIndexChanged;
            // 
            // pnlMultiplier
            // 
            pnlMultiplier.Controls.Add(lblMultiplier);
            pnlMultiplier.Dock = DockStyle.Fill;
            pnlMultiplier.Location = new Point(2, 287);
            pnlMultiplier.Margin = new Padding(2);
            pnlMultiplier.Name = "pnlMultiplier";
            pnlMultiplier.Size = new Size(157, 28);
            pnlMultiplier.TabIndex = 20;
            // 
            // lblMultiplier
            // 
            lblMultiplier.AutoSize = true;
            lblMultiplier.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblMultiplier.ForeColor = Color.White;
            lblMultiplier.Location = new Point(56, 2);
            lblMultiplier.Margin = new Padding(2, 0, 2, 0);
            lblMultiplier.Name = "lblMultiplier";
            lblMultiplier.Size = new Size(68, 17);
            lblMultiplier.TabIndex = 0;
            lblMultiplier.Text = "Multiplier:";
            lblMultiplier.TextAlign = ContentAlignment.MiddleRight;
            // 
            // ddlMultiplier
            // 
            ddlMultiplier.BackColor = Color.Black;
            ddlMultiplier.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlMultiplier.Enabled = false;
            ddlMultiplier.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlMultiplier.ForeColor = Color.White;
            ddlMultiplier.FormattingEnabled = true;
            ddlMultiplier.Location = new Point(163, 287);
            ddlMultiplier.Margin = new Padding(2);
            ddlMultiplier.Name = "ddlMultiplier";
            ddlMultiplier.Size = new Size(229, 24);
            ddlMultiplier.TabIndex = 21;
            // 
            // ddlCurrentlyTraded
            // 
            ddlCurrentlyTraded.BackColor = Color.Black;
            ddlCurrentlyTraded.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlCurrentlyTraded.Enabled = false;
            ddlCurrentlyTraded.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlCurrentlyTraded.ForeColor = Color.White;
            ddlCurrentlyTraded.FormattingEnabled = true;
            ddlCurrentlyTraded.Location = new Point(165, 320);
            ddlCurrentlyTraded.Margin = new Padding(4, 3, 4, 3);
            ddlCurrentlyTraded.Name = "ddlCurrentlyTraded";
            ddlCurrentlyTraded.Size = new Size(228, 24);
            ddlCurrentlyTraded.TabIndex = 22;
            // 
            // pnlCurrentlyTraded
            // 
            pnlCurrentlyTraded.Controls.Add(lblCurrentlyTraded);
            pnlCurrentlyTraded.Dock = DockStyle.Fill;
            pnlCurrentlyTraded.Location = new Point(2, 319);
            pnlCurrentlyTraded.Margin = new Padding(2);
            pnlCurrentlyTraded.Name = "pnlCurrentlyTraded";
            pnlCurrentlyTraded.Size = new Size(157, 28);
            pnlCurrentlyTraded.TabIndex = 23;
            // 
            // lblCurrentlyTraded
            // 
            lblCurrentlyTraded.AutoSize = true;
            lblCurrentlyTraded.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblCurrentlyTraded.ForeColor = Color.White;
            lblCurrentlyTraded.Location = new Point(14, 0);
            lblCurrentlyTraded.Margin = new Padding(4, 0, 4, 0);
            lblCurrentlyTraded.Name = "lblCurrentlyTraded";
            lblCurrentlyTraded.Size = new Size(110, 16);
            lblCurrentlyTraded.TabIndex = 0;
            lblCurrentlyTraded.Text = "Currently Traded:";
            // 
            // FuturesContractEditorControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            Controls.Add(splitContainer1);
            Margin = new Padding(2);
            Name = "FuturesContractEditorControl";
            Size = new Size(1100, 468);
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            pnlFuturesContractIds.ResumeLayout(false);
            pnlFuturesContractIds.PerformLayout();
            tlpFuturesOptionContract.ResumeLayout(false);
            tlpFuturesOptionContract.PerformLayout();
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
            pnlSecurityType.ResumeLayout(false);
            pnlSecurityType.PerformLayout();
            pnlCurrency.ResumeLayout(false);
            pnlCurrency.PerformLayout();
            pnlExchange.ResumeLayout(false);
            pnlExchange.PerformLayout();
            pnlMultiplier.ResumeLayout(false);
            pnlMultiplier.PerformLayout();
            pnlCurrentlyTraded.ResumeLayout(false);
            pnlCurrentlyTraded.PerformLayout();
            ResumeLayout(false);

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
