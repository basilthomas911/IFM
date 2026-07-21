namespace TomasAI.IFM.UI.Net.Views.MarketData
{
    partial class FuturesOptionContractEditorControl
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
            components = new System.ComponentModel.Container();
            pnlEditorSplitter = new SplitContainer();
            lstFuturesOptionContractIds = new ListBox();
            futuresOptionContractViewModelBindingSource = new BindingSource(components);
            pnlFuturesOptionContractIds = new Panel();
            lblFuturesContractContractIds = new Label();
            tlpFuturesOptionContract = new TableLayoutPanel();
            pnlContractId = new Panel();
            lblContractId = new Label();
            txtContractId = new TextBox();
            pnlDescription = new Panel();
            lblDescription = new Label();
            txtDescription = new TextBox();
            pnlContractMonth = new Panel();
            lblContractMonth = new Label();
            dtmContractMonth = new DateTimePicker();
            pnlStrikePrice = new Panel();
            lblStrikePrice = new Label();
            txtStrikePrice = new MaskedTextBox();
            pnlOptionType = new Panel();
            lblOptionType = new Label();
            ddlOptionType = new ComboBox();
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
            ((System.ComponentModel.ISupportInitialize)pnlEditorSplitter).BeginInit();
            pnlEditorSplitter.Panel1.SuspendLayout();
            pnlEditorSplitter.Panel2.SuspendLayout();
            pnlEditorSplitter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)futuresOptionContractViewModelBindingSource).BeginInit();
            pnlFuturesOptionContractIds.SuspendLayout();
            tlpFuturesOptionContract.SuspendLayout();
            pnlContractId.SuspendLayout();
            pnlDescription.SuspendLayout();
            pnlContractMonth.SuspendLayout();
            pnlStrikePrice.SuspendLayout();
            pnlOptionType.SuspendLayout();
            pnlSymbol.SuspendLayout();
            pnlLocalSymbol.SuspendLayout();
            pnlSecurityType.SuspendLayout();
            pnlCurrency.SuspendLayout();
            pnlExchange.SuspendLayout();
            pnlMultiplier.SuspendLayout();
            SuspendLayout();
            // 
            // pnlEditorSplitter
            // 
            pnlEditorSplitter.Dock = DockStyle.Fill;
            pnlEditorSplitter.Location = new Point(0, 0);
            pnlEditorSplitter.Margin = new Padding(2);
            pnlEditorSplitter.Name = "pnlEditorSplitter";
            // 
            // pnlEditorSplitter.Panel1
            // 
            pnlEditorSplitter.Panel1.Controls.Add(lstFuturesOptionContractIds);
            pnlEditorSplitter.Panel1.Controls.Add(pnlFuturesOptionContractIds);
            // 
            // pnlEditorSplitter.Panel2
            // 
            pnlEditorSplitter.Panel2.Controls.Add(tlpFuturesOptionContract);
            pnlEditorSplitter.Size = new Size(1100, 468);
            pnlEditorSplitter.SplitterDistance = 232;
            pnlEditorSplitter.TabIndex = 0;
            // 
            // lstFuturesOptionContractIds
            // 
            lstFuturesOptionContractIds.BackColor = Color.FromArgb(64, 64, 64);
            lstFuturesOptionContractIds.BorderStyle = BorderStyle.FixedSingle;
            lstFuturesOptionContractIds.DataSource = futuresOptionContractViewModelBindingSource;
            lstFuturesOptionContractIds.DisplayMember = "ContractId";
            lstFuturesOptionContractIds.Dock = DockStyle.Fill;
            lstFuturesOptionContractIds.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lstFuturesOptionContractIds.ForeColor = Color.White;
            lstFuturesOptionContractIds.FormattingEnabled = true;
            lstFuturesOptionContractIds.Location = new Point(0, 30);
            lstFuturesOptionContractIds.Margin = new Padding(2);
            lstFuturesOptionContractIds.Name = "lstFuturesOptionContractIds";
            lstFuturesOptionContractIds.Size = new Size(232, 438);
            lstFuturesOptionContractIds.TabIndex = 5;
            lstFuturesOptionContractIds.SelectedIndexChanged += lstFuturesOptionContractIds_SelectedIndexChanged;
            // 
            // pnlFuturesOptionContractIds
            // 
            pnlFuturesOptionContractIds.BackColor = Color.Gray;
            pnlFuturesOptionContractIds.Controls.Add(lblFuturesContractContractIds);
            pnlFuturesOptionContractIds.Dock = DockStyle.Top;
            pnlFuturesOptionContractIds.Location = new Point(0, 0);
            pnlFuturesOptionContractIds.Margin = new Padding(4, 3, 4, 3);
            pnlFuturesOptionContractIds.Name = "pnlFuturesOptionContractIds";
            pnlFuturesOptionContractIds.Size = new Size(232, 30);
            pnlFuturesOptionContractIds.TabIndex = 4;
            // 
            // lblFuturesContractContractIds
            // 
            lblFuturesContractContractIds.AutoSize = true;
            lblFuturesContractContractIds.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblFuturesContractContractIds.ForeColor = Color.Black;
            lblFuturesContractContractIds.Location = new Point(19, 6);
            lblFuturesContractContractIds.Margin = new Padding(4, 0, 4, 0);
            lblFuturesContractContractIds.Name = "lblFuturesContractContractIds";
            lblFuturesContractContractIds.Size = new Size(181, 17);
            lblFuturesContractContractIds.TabIndex = 0;
            lblFuturesContractContractIds.Text = "Futures Option Contract Ids";
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
            tlpFuturesOptionContract.Controls.Add(dtmContractMonth, 1, 2);
            tlpFuturesOptionContract.Controls.Add(pnlStrikePrice, 0, 3);
            tlpFuturesOptionContract.Controls.Add(txtStrikePrice, 1, 3);
            tlpFuturesOptionContract.Controls.Add(pnlOptionType, 0, 4);
            tlpFuturesOptionContract.Controls.Add(ddlOptionType, 1, 4);
            tlpFuturesOptionContract.Controls.Add(pnlSymbol, 0, 5);
            tlpFuturesOptionContract.Controls.Add(ddlSymbol, 1, 5);
            tlpFuturesOptionContract.Controls.Add(pnlLocalSymbol, 0, 6);
            tlpFuturesOptionContract.Controls.Add(txtLocalSymbol, 1, 6);
            tlpFuturesOptionContract.Controls.Add(pnlSecurityType, 0, 7);
            tlpFuturesOptionContract.Controls.Add(ddlSecurityType, 1, 7);
            tlpFuturesOptionContract.Controls.Add(pnlCurrency, 0, 8);
            tlpFuturesOptionContract.Controls.Add(ddlCurrency, 1, 8);
            tlpFuturesOptionContract.Controls.Add(pnlExchange, 0, 9);
            tlpFuturesOptionContract.Controls.Add(ddlExchange, 1, 9);
            tlpFuturesOptionContract.Controls.Add(pnlMultiplier, 0, 10);
            tlpFuturesOptionContract.Controls.Add(ddlMultiplier, 1, 10);
            tlpFuturesOptionContract.Dock = DockStyle.Fill;
            tlpFuturesOptionContract.Location = new Point(0, 0);
            tlpFuturesOptionContract.Margin = new Padding(2);
            tlpFuturesOptionContract.Name = "tlpFuturesOptionContract";
            tlpFuturesOptionContract.RowCount = 12;
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 59F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 29F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 29F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tlpFuturesOptionContract.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlpFuturesOptionContract.Size = new Size(864, 468);
            tlpFuturesOptionContract.TabIndex = 0;
            // 
            // pnlContractId
            // 
            pnlContractId.BackColor = Color.FromArgb(64, 64, 64);
            pnlContractId.Controls.Add(lblContractId);
            pnlContractId.Dock = DockStyle.Fill;
            pnlContractId.Location = new Point(0, 0);
            pnlContractId.Margin = new Padding(0);
            pnlContractId.Name = "pnlContractId";
            pnlContractId.Size = new Size(161, 31);
            pnlContractId.TabIndex = 0;
            // 
            // lblContractId
            // 
            lblContractId.AutoSize = true;
            lblContractId.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblContractId.ForeColor = Color.White;
            lblContractId.Location = new Point(62, 5);
            lblContractId.Margin = new Padding(2, 0, 2, 0);
            lblContractId.Name = "lblContractId";
            lblContractId.Size = new Size(80, 17);
            lblContractId.TabIndex = 0;
            lblContractId.Text = "Contract Id:";
            // 
            // txtContractId
            // 
            txtContractId.BackColor = Color.Black;
            txtContractId.BorderStyle = BorderStyle.FixedSingle;
            txtContractId.Dock = DockStyle.Left;
            txtContractId.Enabled = false;
            txtContractId.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtContractId.ForeColor = Color.Gray;
            txtContractId.Location = new Point(163, 2);
            txtContractId.Margin = new Padding(2);
            txtContractId.Name = "txtContractId";
            txtContractId.Size = new Size(229, 23);
            txtContractId.TabIndex = 1;
            txtContractId.Text = "1234";
            // 
            // pnlDescription
            // 
            pnlDescription.BackColor = Color.FromArgb(64, 64, 64);
            pnlDescription.Controls.Add(lblDescription);
            pnlDescription.Dock = DockStyle.Fill;
            pnlDescription.Location = new Point(2, 33);
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
            lblDescription.Location = new Point(55, 2);
            lblDescription.Margin = new Padding(2, 0, 2, 0);
            lblDescription.Name = "lblDescription";
            lblDescription.Size = new Size(83, 17);
            lblDescription.TabIndex = 0;
            lblDescription.Text = "Description:";
            // 
            // txtDescription
            // 
            txtDescription.BackColor = Color.Black;
            txtDescription.BorderStyle = BorderStyle.FixedSingle;
            txtDescription.Dock = DockStyle.Fill;
            txtDescription.Enabled = false;
            txtDescription.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtDescription.ForeColor = Color.Gray;
            txtDescription.Location = new Point(163, 33);
            txtDescription.Margin = new Padding(2);
            txtDescription.Multiline = true;
            txtDescription.Name = "txtDescription";
            txtDescription.Size = new Size(699, 55);
            txtDescription.TabIndex = 3;
            txtDescription.Text = "5678";
            // 
            // pnlContractMonth
            // 
            pnlContractMonth.BackColor = Color.FromArgb(64, 64, 64);
            pnlContractMonth.Controls.Add(lblContractMonth);
            pnlContractMonth.Dock = DockStyle.Fill;
            pnlContractMonth.Location = new Point(2, 92);
            pnlContractMonth.Margin = new Padding(2);
            pnlContractMonth.Name = "pnlContractMonth";
            pnlContractMonth.Size = new Size(157, 24);
            pnlContractMonth.TabIndex = 4;
            // 
            // lblContractMonth
            // 
            lblContractMonth.AutoSize = true;
            lblContractMonth.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblContractMonth.ForeColor = Color.White;
            lblContractMonth.Location = new Point(31, 4);
            lblContractMonth.Margin = new Padding(2, 0, 2, 0);
            lblContractMonth.Name = "lblContractMonth";
            lblContractMonth.Size = new Size(108, 17);
            lblContractMonth.TabIndex = 0;
            lblContractMonth.Text = "Contract Month:";
            // 
            // dtmContractMonth
            // 
            dtmContractMonth.Dock = DockStyle.Left;
            dtmContractMonth.Enabled = false;
            dtmContractMonth.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dtmContractMonth.Format = DateTimePickerFormat.Short;
            dtmContractMonth.Location = new Point(163, 92);
            dtmContractMonth.Margin = new Padding(2);
            dtmContractMonth.Name = "dtmContractMonth";
            dtmContractMonth.Size = new Size(229, 23);
            dtmContractMonth.TabIndex = 5;
            dtmContractMonth.ValueChanged += dtmContractMonth_ValueChanged;
            // 
            // pnlStrikePrice
            // 
            pnlStrikePrice.BackColor = Color.FromArgb(64, 64, 64);
            pnlStrikePrice.Controls.Add(lblStrikePrice);
            pnlStrikePrice.Dock = DockStyle.Fill;
            pnlStrikePrice.Location = new Point(2, 120);
            pnlStrikePrice.Margin = new Padding(2);
            pnlStrikePrice.Name = "pnlStrikePrice";
            pnlStrikePrice.Size = new Size(157, 24);
            pnlStrikePrice.TabIndex = 6;
            // 
            // lblStrikePrice
            // 
            lblStrikePrice.AutoSize = true;
            lblStrikePrice.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblStrikePrice.ForeColor = Color.White;
            lblStrikePrice.Location = new Point(54, 2);
            lblStrikePrice.Margin = new Padding(2, 0, 2, 0);
            lblStrikePrice.Name = "lblStrikePrice";
            lblStrikePrice.Size = new Size(84, 17);
            lblStrikePrice.TabIndex = 0;
            lblStrikePrice.Text = "Strike Price:";
            // 
            // txtStrikePrice
            // 
            txtStrikePrice.Dock = DockStyle.Left;
            txtStrikePrice.Enabled = false;
            txtStrikePrice.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtStrikePrice.Location = new Point(163, 120);
            txtStrikePrice.Margin = new Padding(2);
            txtStrikePrice.Mask = "0000";
            txtStrikePrice.Name = "txtStrikePrice";
            txtStrikePrice.Size = new Size(114, 23);
            txtStrikePrice.TabIndex = 7;
            txtStrikePrice.ValidatingType = typeof(int);
            txtStrikePrice.Leave += txtStrikePrice_Leave;
            // 
            // pnlOptionType
            // 
            pnlOptionType.BackColor = Color.FromArgb(64, 64, 64);
            pnlOptionType.Controls.Add(lblOptionType);
            pnlOptionType.Dock = DockStyle.Fill;
            pnlOptionType.Location = new Point(2, 148);
            pnlOptionType.Margin = new Padding(2);
            pnlOptionType.Name = "pnlOptionType";
            pnlOptionType.Size = new Size(157, 26);
            pnlOptionType.TabIndex = 8;
            // 
            // lblOptionType
            // 
            lblOptionType.AutoSize = true;
            lblOptionType.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblOptionType.ForeColor = Color.White;
            lblOptionType.Location = new Point(52, 2);
            lblOptionType.Margin = new Padding(2, 0, 2, 0);
            lblOptionType.Name = "lblOptionType";
            lblOptionType.Size = new Size(86, 17);
            lblOptionType.TabIndex = 0;
            lblOptionType.Text = "OptionType:";
            // 
            // ddlOptionType
            // 
            ddlOptionType.BackColor = Color.White;
            ddlOptionType.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlOptionType.Enabled = false;
            ddlOptionType.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlOptionType.ForeColor = Color.Black;
            ddlOptionType.FormattingEnabled = true;
            ddlOptionType.Location = new Point(163, 148);
            ddlOptionType.Margin = new Padding(2);
            ddlOptionType.Name = "ddlOptionType";
            ddlOptionType.Size = new Size(229, 24);
            ddlOptionType.TabIndex = 9;
            ddlOptionType.SelectedIndexChanged += ddlOptionType_SelectedIndexChanged;
            // 
            // pnlSymbol
            // 
            pnlSymbol.BackColor = Color.FromArgb(64, 64, 64);
            pnlSymbol.Controls.Add(lblSymbol);
            pnlSymbol.Dock = DockStyle.Fill;
            pnlSymbol.Location = new Point(2, 178);
            pnlSymbol.Margin = new Padding(2);
            pnlSymbol.Name = "pnlSymbol";
            pnlSymbol.Size = new Size(157, 28);
            pnlSymbol.TabIndex = 10;
            // 
            // lblSymbol
            // 
            lblSymbol.AutoSize = true;
            lblSymbol.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblSymbol.ForeColor = Color.White;
            lblSymbol.Location = new Point(82, 2);
            lblSymbol.Margin = new Padding(2, 0, 2, 0);
            lblSymbol.Name = "lblSymbol";
            lblSymbol.Size = new Size(58, 17);
            lblSymbol.TabIndex = 0;
            lblSymbol.Text = "Symbol:";
            // 
            // ddlSymbol
            // 
            ddlSymbol.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlSymbol.Enabled = false;
            ddlSymbol.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlSymbol.FormattingEnabled = true;
            ddlSymbol.Location = new Point(163, 178);
            ddlSymbol.Margin = new Padding(2);
            ddlSymbol.Name = "ddlSymbol";
            ddlSymbol.Size = new Size(229, 24);
            ddlSymbol.TabIndex = 11;
            ddlSymbol.SelectedIndexChanged += ddlSymbol_SelectedIndexChanged;
            // 
            // pnlLocalSymbol
            // 
            pnlLocalSymbol.BackColor = Color.FromArgb(64, 64, 64);
            pnlLocalSymbol.Controls.Add(lblLocalSymbol);
            pnlLocalSymbol.Dock = DockStyle.Fill;
            pnlLocalSymbol.Location = new Point(2, 210);
            pnlLocalSymbol.Margin = new Padding(2);
            pnlLocalSymbol.Name = "pnlLocalSymbol";
            pnlLocalSymbol.Size = new Size(157, 25);
            pnlLocalSymbol.TabIndex = 12;
            // 
            // lblLocalSymbol
            // 
            lblLocalSymbol.AutoSize = true;
            lblLocalSymbol.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblLocalSymbol.ForeColor = Color.White;
            lblLocalSymbol.Location = new Point(41, 2);
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
            txtLocalSymbol.ForeColor = Color.Gray;
            txtLocalSymbol.Location = new Point(163, 210);
            txtLocalSymbol.Margin = new Padding(2);
            txtLocalSymbol.Name = "txtLocalSymbol";
            txtLocalSymbol.Size = new Size(114, 23);
            txtLocalSymbol.TabIndex = 13;
            txtLocalSymbol.Text = "2468";
            // 
            // pnlSecurityType
            // 
            pnlSecurityType.BackColor = Color.FromArgb(64, 64, 64);
            pnlSecurityType.Controls.Add(lblSecurityType);
            pnlSecurityType.Dock = DockStyle.Fill;
            pnlSecurityType.Location = new Point(2, 239);
            pnlSecurityType.Margin = new Padding(2);
            pnlSecurityType.Name = "pnlSecurityType";
            pnlSecurityType.Size = new Size(157, 26);
            pnlSecurityType.TabIndex = 14;
            // 
            // lblSecurityType
            // 
            lblSecurityType.AutoSize = true;
            lblSecurityType.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblSecurityType.ForeColor = Color.White;
            lblSecurityType.Location = new Point(37, 2);
            lblSecurityType.Margin = new Padding(2, 0, 2, 0);
            lblSecurityType.Name = "lblSecurityType";
            lblSecurityType.Size = new Size(99, 17);
            lblSecurityType.TabIndex = 0;
            lblSecurityType.Text = "Security Type:";
            // 
            // ddlSecurityType
            // 
            ddlSecurityType.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlSecurityType.Enabled = false;
            ddlSecurityType.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlSecurityType.FormattingEnabled = true;
            ddlSecurityType.Location = new Point(163, 239);
            ddlSecurityType.Margin = new Padding(2);
            ddlSecurityType.Name = "ddlSecurityType";
            ddlSecurityType.Size = new Size(229, 24);
            ddlSecurityType.TabIndex = 15;
            // 
            // pnlCurrency
            // 
            pnlCurrency.BackColor = Color.FromArgb(64, 64, 64);
            pnlCurrency.Controls.Add(lblCurrency);
            pnlCurrency.Dock = DockStyle.Fill;
            pnlCurrency.Location = new Point(2, 269);
            pnlCurrency.Margin = new Padding(2);
            pnlCurrency.Name = "pnlCurrency";
            pnlCurrency.Size = new Size(157, 25);
            pnlCurrency.TabIndex = 16;
            // 
            // lblCurrency
            // 
            lblCurrency.AutoSize = true;
            lblCurrency.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblCurrency.ForeColor = Color.White;
            lblCurrency.Location = new Point(70, 2);
            lblCurrency.Margin = new Padding(2, 0, 2, 0);
            lblCurrency.Name = "lblCurrency";
            lblCurrency.Size = new Size(69, 17);
            lblCurrency.TabIndex = 0;
            lblCurrency.Text = "Currency:";
            // 
            // ddlCurrency
            // 
            ddlCurrency.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlCurrency.Enabled = false;
            ddlCurrency.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlCurrency.FormattingEnabled = true;
            ddlCurrency.Location = new Point(163, 269);
            ddlCurrency.Margin = new Padding(2);
            ddlCurrency.Name = "ddlCurrency";
            ddlCurrency.Size = new Size(229, 24);
            ddlCurrency.TabIndex = 17;
            // 
            // pnlExchange
            // 
            pnlExchange.BackColor = Color.FromArgb(64, 64, 64);
            pnlExchange.Controls.Add(lblExchange);
            pnlExchange.Dock = DockStyle.Fill;
            pnlExchange.Location = new Point(2, 298);
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
            lblExchange.Location = new Point(65, 2);
            lblExchange.Margin = new Padding(2, 0, 2, 0);
            lblExchange.Name = "lblExchange";
            lblExchange.Size = new Size(74, 17);
            lblExchange.TabIndex = 0;
            lblExchange.Text = "Exchange:";
            // 
            // ddlExchange
            // 
            ddlExchange.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlExchange.Enabled = false;
            ddlExchange.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlExchange.FormattingEnabled = true;
            ddlExchange.Location = new Point(163, 298);
            ddlExchange.Margin = new Padding(2);
            ddlExchange.Name = "ddlExchange";
            ddlExchange.Size = new Size(229, 24);
            ddlExchange.TabIndex = 19;
            ddlExchange.SelectedIndexChanged += ddlExchange_SelectedIndexChanged;
            // 
            // pnlMultiplier
            // 
            pnlMultiplier.BackColor = Color.FromArgb(64, 64, 64);
            pnlMultiplier.Controls.Add(lblMultiplier);
            pnlMultiplier.Dock = DockStyle.Fill;
            pnlMultiplier.Location = new Point(2, 328);
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
            lblMultiplier.Location = new Point(70, 2);
            lblMultiplier.Margin = new Padding(2, 0, 2, 0);
            lblMultiplier.Name = "lblMultiplier";
            lblMultiplier.Size = new Size(68, 17);
            lblMultiplier.TabIndex = 0;
            lblMultiplier.Text = "Multiplier:";
            // 
            // ddlMultiplier
            // 
            ddlMultiplier.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlMultiplier.Enabled = false;
            ddlMultiplier.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlMultiplier.FormattingEnabled = true;
            ddlMultiplier.Location = new Point(163, 328);
            ddlMultiplier.Margin = new Padding(2);
            ddlMultiplier.Name = "ddlMultiplier";
            ddlMultiplier.Size = new Size(229, 24);
            ddlMultiplier.TabIndex = 21;
            // 
            // FuturesOptionContractEditorControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            Controls.Add(pnlEditorSplitter);
            Margin = new Padding(2);
            Name = "FuturesOptionContractEditorControl";
            Size = new Size(1100, 468);
            pnlEditorSplitter.Panel1.ResumeLayout(false);
            pnlEditorSplitter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pnlEditorSplitter).EndInit();
            pnlEditorSplitter.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)futuresOptionContractViewModelBindingSource).EndInit();
            pnlFuturesOptionContractIds.ResumeLayout(false);
            pnlFuturesOptionContractIds.PerformLayout();
            tlpFuturesOptionContract.ResumeLayout(false);
            tlpFuturesOptionContract.PerformLayout();
            pnlContractId.ResumeLayout(false);
            pnlContractId.PerformLayout();
            pnlDescription.ResumeLayout(false);
            pnlDescription.PerformLayout();
            pnlContractMonth.ResumeLayout(false);
            pnlContractMonth.PerformLayout();
            pnlStrikePrice.ResumeLayout(false);
            pnlStrikePrice.PerformLayout();
            pnlOptionType.ResumeLayout(false);
            pnlOptionType.PerformLayout();
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
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer pnlEditorSplitter;
        private System.Windows.Forms.TableLayoutPanel tlpFuturesOptionContract;
        private System.Windows.Forms.Panel pnlContractId;
        private System.Windows.Forms.Label lblContractId;
        private System.Windows.Forms.TextBox txtContractId;
        private System.Windows.Forms.Panel pnlDescription;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.Panel pnlContractMonth;
        private System.Windows.Forms.Label lblContractMonth;
        private System.Windows.Forms.DateTimePicker dtmContractMonth;
        private System.Windows.Forms.Panel pnlStrikePrice;
        private System.Windows.Forms.Label lblStrikePrice;
        private System.Windows.Forms.MaskedTextBox txtStrikePrice;
        private System.Windows.Forms.Panel pnlOptionType;
        private System.Windows.Forms.Label lblOptionType;
        private System.Windows.Forms.ComboBox ddlOptionType;
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
        private System.Windows.Forms.BindingSource futuresOptionContractViewModelBindingSource;
        private System.Windows.Forms.ListBox lstFuturesOptionContractIds;
        private System.Windows.Forms.Panel pnlFuturesOptionContractIds;
        private System.Windows.Forms.Label lblFuturesContractContractIds;
    }
}
