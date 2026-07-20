namespace TomasAI.IFM.Views.MarketData
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
            this.components = new System.ComponentModel.Container();
            this.pnlEditorSplitter = new System.Windows.Forms.SplitContainer();
            this.lstFuturesOptionContractIds = new System.Windows.Forms.ListBox();
            this.futuresOptionContractViewModelBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.pnlFuturesOptionContractIds = new System.Windows.Forms.Panel();
            this.lblFuturesContractContractIds = new System.Windows.Forms.Label();
            this.tlpFuturesOptionContract = new System.Windows.Forms.TableLayoutPanel();
            this.pnlContractId = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.txtContractId = new System.Windows.Forms.TextBox();
            this.pnlDescription = new System.Windows.Forms.Panel();
            this.lblDescription = new System.Windows.Forms.Label();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.pnlContractMonth = new System.Windows.Forms.Panel();
            this.lblContractMonth = new System.Windows.Forms.Label();
            this.dtmContractMonth = new System.Windows.Forms.DateTimePicker();
            this.pnlStrikePrice = new System.Windows.Forms.Panel();
            this.lblStrikePrice = new System.Windows.Forms.Label();
            this.txtStrikePrice = new System.Windows.Forms.MaskedTextBox();
            this.pnlOptionType = new System.Windows.Forms.Panel();
            this.lblOptionType = new System.Windows.Forms.Label();
            this.ddlOptionType = new System.Windows.Forms.ComboBox();
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
            ((System.ComponentModel.ISupportInitialize)(this.pnlEditorSplitter)).BeginInit();
            this.pnlEditorSplitter.Panel1.SuspendLayout();
            this.pnlEditorSplitter.Panel2.SuspendLayout();
            this.pnlEditorSplitter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.futuresOptionContractViewModelBindingSource)).BeginInit();
            this.pnlFuturesOptionContractIds.SuspendLayout();
            this.tlpFuturesOptionContract.SuspendLayout();
            this.pnlContractId.SuspendLayout();
            this.pnlDescription.SuspendLayout();
            this.pnlContractMonth.SuspendLayout();
            this.pnlStrikePrice.SuspendLayout();
            this.pnlOptionType.SuspendLayout();
            this.pnlSymbol.SuspendLayout();
            this.pnlLocalSymbol.SuspendLayout();
            this.pnlSecurityType.SuspendLayout();
            this.pnlCurrency.SuspendLayout();
            this.pnlExchange.SuspendLayout();
            this.pnlMultiplier.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlEditorSplitter
            // 
            this.pnlEditorSplitter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlEditorSplitter.Location = new System.Drawing.Point(0, 0);
            this.pnlEditorSplitter.Margin = new System.Windows.Forms.Padding(2);
            this.pnlEditorSplitter.Name = "pnlEditorSplitter";
            // 
            // pnlEditorSplitter.Panel1
            // 
            this.pnlEditorSplitter.Panel1.Controls.Add(this.lstFuturesOptionContractIds);
            this.pnlEditorSplitter.Panel1.Controls.Add(this.pnlFuturesOptionContractIds);
            // 
            // pnlEditorSplitter.Panel2
            // 
            this.pnlEditorSplitter.Panel2.Controls.Add(this.tlpFuturesOptionContract);
            this.pnlEditorSplitter.Size = new System.Drawing.Size(943, 406);
            this.pnlEditorSplitter.SplitterDistance = 200;
            this.pnlEditorSplitter.SplitterWidth = 3;
            this.pnlEditorSplitter.TabIndex = 0;
            // 
            // lstFuturesOptionContractIds
            // 
            this.lstFuturesOptionContractIds.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lstFuturesOptionContractIds.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lstFuturesOptionContractIds.DataSource = this.futuresOptionContractViewModelBindingSource;
            this.lstFuturesOptionContractIds.DisplayMember = "ContractId";
            this.lstFuturesOptionContractIds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstFuturesOptionContractIds.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstFuturesOptionContractIds.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lstFuturesOptionContractIds.FormattingEnabled = true;
            this.lstFuturesOptionContractIds.ItemHeight = 16;
            this.lstFuturesOptionContractIds.Location = new System.Drawing.Point(0, 26);
            this.lstFuturesOptionContractIds.Margin = new System.Windows.Forms.Padding(2);
            this.lstFuturesOptionContractIds.Name = "lstFuturesOptionContractIds";
            this.lstFuturesOptionContractIds.Size = new System.Drawing.Size(200, 380);
            this.lstFuturesOptionContractIds.TabIndex = 5;
            this.lstFuturesOptionContractIds.SelectedIndexChanged += new System.EventHandler(this.lstFuturesOptionContractIds_SelectedIndexChanged);
            // 
            // futuresOptionContractViewModelBindingSource
            // 
            this.futuresOptionContractViewModelBindingSource.DataSource = typeof(TomasAI.IFM.Shared.MarketData.ViewModels.FuturesOptionContractReadModel);
            // 
            // pnlFuturesOptionContractIds
            // 
            this.pnlFuturesOptionContractIds.BackColor = System.Drawing.Color.Gray;
            this.pnlFuturesOptionContractIds.Controls.Add(this.lblFuturesContractContractIds);
            this.pnlFuturesOptionContractIds.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlFuturesOptionContractIds.Location = new System.Drawing.Point(0, 0);
            this.pnlFuturesOptionContractIds.Name = "pnlFuturesOptionContractIds";
            this.pnlFuturesOptionContractIds.Size = new System.Drawing.Size(200, 26);
            this.pnlFuturesOptionContractIds.TabIndex = 4;
            // 
            // lblFuturesContractContractIds
            // 
            this.lblFuturesContractContractIds.AutoSize = true;
            this.lblFuturesContractContractIds.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFuturesContractContractIds.Location = new System.Drawing.Point(16, 5);
            this.lblFuturesContractContractIds.Name = "lblFuturesContractContractIds";
            this.lblFuturesContractContractIds.Size = new System.Drawing.Size(181, 17);
            this.lblFuturesContractContractIds.TabIndex = 0;
            this.lblFuturesContractContractIds.Text = "Futures Option Contract Ids";
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
            this.tlpFuturesOptionContract.Controls.Add(this.dtmContractMonth, 1, 2);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlStrikePrice, 0, 3);
            this.tlpFuturesOptionContract.Controls.Add(this.txtStrikePrice, 1, 3);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlOptionType, 0, 4);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlOptionType, 1, 4);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlSymbol, 0, 5);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlSymbol, 1, 5);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlLocalSymbol, 0, 6);
            this.tlpFuturesOptionContract.Controls.Add(this.txtLocalSymbol, 1, 6);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlSecurityType, 0, 7);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlSecurityType, 1, 7);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlCurrency, 0, 8);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlCurrency, 1, 8);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlExchange, 0, 9);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlExchange, 1, 9);
            this.tlpFuturesOptionContract.Controls.Add(this.pnlMultiplier, 0, 10);
            this.tlpFuturesOptionContract.Controls.Add(this.ddlMultiplier, 1, 10);
            this.tlpFuturesOptionContract.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpFuturesOptionContract.Location = new System.Drawing.Point(0, 0);
            this.tlpFuturesOptionContract.Margin = new System.Windows.Forms.Padding(2);
            this.tlpFuturesOptionContract.Name = "tlpFuturesOptionContract";
            this.tlpFuturesOptionContract.RowCount = 12;
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 27F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 51F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tlpFuturesOptionContract.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpFuturesOptionContract.Size = new System.Drawing.Size(740, 406);
            this.tlpFuturesOptionContract.TabIndex = 0;
            // 
            // pnlContractId
            // 
            this.pnlContractId.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlContractId.Controls.Add(this.label1);
            this.pnlContractId.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContractId.Location = new System.Drawing.Point(0, 0);
            this.pnlContractId.Margin = new System.Windows.Forms.Padding(0);
            this.pnlContractId.Name = "pnlContractId";
            this.pnlContractId.Size = new System.Drawing.Size(138, 27);
            this.pnlContractId.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.label1.Location = new System.Drawing.Point(53, 4);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "Contract Id:";
            // 
            // txtContractId
            // 
            this.txtContractId.Dock = System.Windows.Forms.DockStyle.Left;
            this.txtContractId.Enabled = false;
            this.txtContractId.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtContractId.Location = new System.Drawing.Point(140, 2);
            this.txtContractId.Margin = new System.Windows.Forms.Padding(2);
            this.txtContractId.Name = "txtContractId";
            this.txtContractId.Size = new System.Drawing.Size(197, 23);
            this.txtContractId.TabIndex = 1;
            // 
            // pnlDescription
            // 
            this.pnlDescription.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlDescription.Controls.Add(this.lblDescription);
            this.pnlDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlDescription.Location = new System.Drawing.Point(2, 29);
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
            this.lblDescription.Location = new System.Drawing.Point(47, 2);
            this.lblDescription.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(83, 17);
            this.lblDescription.TabIndex = 0;
            this.lblDescription.Text = "Description:";
            // 
            // txtDescription
            // 
            this.txtDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDescription.Enabled = false;
            this.txtDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDescription.Location = new System.Drawing.Point(140, 29);
            this.txtDescription.Margin = new System.Windows.Forms.Padding(2);
            this.txtDescription.Multiline = true;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.Size = new System.Drawing.Size(598, 47);
            this.txtDescription.TabIndex = 3;
            // 
            // pnlContractMonth
            // 
            this.pnlContractMonth.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlContractMonth.Controls.Add(this.lblContractMonth);
            this.pnlContractMonth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContractMonth.Location = new System.Drawing.Point(2, 80);
            this.pnlContractMonth.Margin = new System.Windows.Forms.Padding(2);
            this.pnlContractMonth.Name = "pnlContractMonth";
            this.pnlContractMonth.Size = new System.Drawing.Size(134, 20);
            this.pnlContractMonth.TabIndex = 4;
            // 
            // lblContractMonth
            // 
            this.lblContractMonth.AutoSize = true;
            this.lblContractMonth.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblContractMonth.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblContractMonth.Location = new System.Drawing.Point(23, 4);
            this.lblContractMonth.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblContractMonth.Name = "lblContractMonth";
            this.lblContractMonth.Size = new System.Drawing.Size(108, 17);
            this.lblContractMonth.TabIndex = 0;
            this.lblContractMonth.Text = "Contract Month:";
            // 
            // dtmContractMonth
            // 
            this.dtmContractMonth.Dock = System.Windows.Forms.DockStyle.Left;
            this.dtmContractMonth.Enabled = false;
            this.dtmContractMonth.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtmContractMonth.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtmContractMonth.Location = new System.Drawing.Point(140, 80);
            this.dtmContractMonth.Margin = new System.Windows.Forms.Padding(2);
            this.dtmContractMonth.Name = "dtmContractMonth";
            this.dtmContractMonth.Size = new System.Drawing.Size(197, 23);
            this.dtmContractMonth.TabIndex = 5;
            this.dtmContractMonth.ValueChanged += new System.EventHandler(this.dtmContractMonth_ValueChanged);
            // 
            // pnlStrikePrice
            // 
            this.pnlStrikePrice.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlStrikePrice.Controls.Add(this.lblStrikePrice);
            this.pnlStrikePrice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStrikePrice.Location = new System.Drawing.Point(2, 104);
            this.pnlStrikePrice.Margin = new System.Windows.Forms.Padding(2);
            this.pnlStrikePrice.Name = "pnlStrikePrice";
            this.pnlStrikePrice.Size = new System.Drawing.Size(134, 20);
            this.pnlStrikePrice.TabIndex = 6;
            // 
            // lblStrikePrice
            // 
            this.lblStrikePrice.AutoSize = true;
            this.lblStrikePrice.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblStrikePrice.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblStrikePrice.Location = new System.Drawing.Point(46, 2);
            this.lblStrikePrice.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblStrikePrice.Name = "lblStrikePrice";
            this.lblStrikePrice.Size = new System.Drawing.Size(84, 17);
            this.lblStrikePrice.TabIndex = 0;
            this.lblStrikePrice.Text = "Strike Price:";
            // 
            // txtStrikePrice
            // 
            this.txtStrikePrice.Dock = System.Windows.Forms.DockStyle.Left;
            this.txtStrikePrice.Enabled = false;
            this.txtStrikePrice.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtStrikePrice.Location = new System.Drawing.Point(140, 104);
            this.txtStrikePrice.Margin = new System.Windows.Forms.Padding(2);
            this.txtStrikePrice.Mask = "0000";
            this.txtStrikePrice.Name = "txtStrikePrice";
            this.txtStrikePrice.Size = new System.Drawing.Size(98, 23);
            this.txtStrikePrice.TabIndex = 7;
            this.txtStrikePrice.ValidatingType = typeof(int);
            this.txtStrikePrice.Leave += new System.EventHandler(this.txtStrikePrice_Leave);
            // 
            // pnlOptionType
            // 
            this.pnlOptionType.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlOptionType.Controls.Add(this.lblOptionType);
            this.pnlOptionType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlOptionType.Location = new System.Drawing.Point(2, 128);
            this.pnlOptionType.Margin = new System.Windows.Forms.Padding(2);
            this.pnlOptionType.Name = "pnlOptionType";
            this.pnlOptionType.Size = new System.Drawing.Size(134, 22);
            this.pnlOptionType.TabIndex = 8;
            // 
            // lblOptionType
            // 
            this.lblOptionType.AutoSize = true;
            this.lblOptionType.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblOptionType.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblOptionType.Location = new System.Drawing.Point(45, 2);
            this.lblOptionType.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblOptionType.Name = "lblOptionType";
            this.lblOptionType.Size = new System.Drawing.Size(86, 17);
            this.lblOptionType.TabIndex = 0;
            this.lblOptionType.Text = "OptionType:";
            // 
            // ddlOptionType
            // 
            this.ddlOptionType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlOptionType.Enabled = false;
            this.ddlOptionType.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlOptionType.FormattingEnabled = true;
            this.ddlOptionType.Location = new System.Drawing.Point(140, 128);
            this.ddlOptionType.Margin = new System.Windows.Forms.Padding(2);
            this.ddlOptionType.Name = "ddlOptionType";
            this.ddlOptionType.Size = new System.Drawing.Size(197, 24);
            this.ddlOptionType.TabIndex = 9;
            this.ddlOptionType.SelectedIndexChanged += new System.EventHandler(this.ddlOptionType_SelectedIndexChanged);
            // 
            // pnlSymbol
            // 
            this.pnlSymbol.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlSymbol.Controls.Add(this.lblSymbol);
            this.pnlSymbol.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSymbol.Location = new System.Drawing.Point(2, 154);
            this.pnlSymbol.Margin = new System.Windows.Forms.Padding(2);
            this.pnlSymbol.Name = "pnlSymbol";
            this.pnlSymbol.Size = new System.Drawing.Size(134, 24);
            this.pnlSymbol.TabIndex = 10;
            // 
            // lblSymbol
            // 
            this.lblSymbol.AutoSize = true;
            this.lblSymbol.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSymbol.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblSymbol.Location = new System.Drawing.Point(70, 2);
            this.lblSymbol.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblSymbol.Name = "lblSymbol";
            this.lblSymbol.Size = new System.Drawing.Size(58, 17);
            this.lblSymbol.TabIndex = 0;
            this.lblSymbol.Text = "Symbol:";
            // 
            // ddlSymbol
            // 
            this.ddlSymbol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlSymbol.Enabled = false;
            this.ddlSymbol.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlSymbol.FormattingEnabled = true;
            this.ddlSymbol.Location = new System.Drawing.Point(140, 154);
            this.ddlSymbol.Margin = new System.Windows.Forms.Padding(2);
            this.ddlSymbol.Name = "ddlSymbol";
            this.ddlSymbol.Size = new System.Drawing.Size(197, 24);
            this.ddlSymbol.TabIndex = 11;
            this.ddlSymbol.SelectedIndexChanged += new System.EventHandler(this.ddlSymbol_SelectedIndexChanged);
            // 
            // pnlLocalSymbol
            // 
            this.pnlLocalSymbol.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlLocalSymbol.Controls.Add(this.lblLocalSymbol);
            this.pnlLocalSymbol.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLocalSymbol.Location = new System.Drawing.Point(2, 182);
            this.pnlLocalSymbol.Margin = new System.Windows.Forms.Padding(2);
            this.pnlLocalSymbol.Name = "pnlLocalSymbol";
            this.pnlLocalSymbol.Size = new System.Drawing.Size(134, 21);
            this.pnlLocalSymbol.TabIndex = 12;
            // 
            // lblLocalSymbol
            // 
            this.lblLocalSymbol.AutoSize = true;
            this.lblLocalSymbol.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLocalSymbol.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblLocalSymbol.Location = new System.Drawing.Point(35, 2);
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
            this.txtLocalSymbol.Location = new System.Drawing.Point(140, 182);
            this.txtLocalSymbol.Margin = new System.Windows.Forms.Padding(2);
            this.txtLocalSymbol.Name = "txtLocalSymbol";
            this.txtLocalSymbol.Size = new System.Drawing.Size(98, 23);
            this.txtLocalSymbol.TabIndex = 13;
            // 
            // pnlSecurityType
            // 
            this.pnlSecurityType.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlSecurityType.Controls.Add(this.lblSecurityType);
            this.pnlSecurityType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSecurityType.Location = new System.Drawing.Point(2, 207);
            this.pnlSecurityType.Margin = new System.Windows.Forms.Padding(2);
            this.pnlSecurityType.Name = "pnlSecurityType";
            this.pnlSecurityType.Size = new System.Drawing.Size(134, 22);
            this.pnlSecurityType.TabIndex = 14;
            // 
            // lblSecurityType
            // 
            this.lblSecurityType.AutoSize = true;
            this.lblSecurityType.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSecurityType.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblSecurityType.Location = new System.Drawing.Point(32, 2);
            this.lblSecurityType.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblSecurityType.Name = "lblSecurityType";
            this.lblSecurityType.Size = new System.Drawing.Size(99, 17);
            this.lblSecurityType.TabIndex = 0;
            this.lblSecurityType.Text = "Security Type:";
            // 
            // ddlSecurityType
            // 
            this.ddlSecurityType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlSecurityType.Enabled = false;
            this.ddlSecurityType.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlSecurityType.FormattingEnabled = true;
            this.ddlSecurityType.Location = new System.Drawing.Point(140, 207);
            this.ddlSecurityType.Margin = new System.Windows.Forms.Padding(2);
            this.ddlSecurityType.Name = "ddlSecurityType";
            this.ddlSecurityType.Size = new System.Drawing.Size(197, 24);
            this.ddlSecurityType.TabIndex = 15;
            // 
            // pnlCurrency
            // 
            this.pnlCurrency.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlCurrency.Controls.Add(this.lblCurrency);
            this.pnlCurrency.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlCurrency.Location = new System.Drawing.Point(2, 233);
            this.pnlCurrency.Margin = new System.Windows.Forms.Padding(2);
            this.pnlCurrency.Name = "pnlCurrency";
            this.pnlCurrency.Size = new System.Drawing.Size(134, 21);
            this.pnlCurrency.TabIndex = 16;
            // 
            // lblCurrency
            // 
            this.lblCurrency.AutoSize = true;
            this.lblCurrency.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCurrency.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblCurrency.Location = new System.Drawing.Point(60, 2);
            this.lblCurrency.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblCurrency.Name = "lblCurrency";
            this.lblCurrency.Size = new System.Drawing.Size(69, 17);
            this.lblCurrency.TabIndex = 0;
            this.lblCurrency.Text = "Currency:";
            // 
            // ddlCurrency
            // 
            this.ddlCurrency.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlCurrency.Enabled = false;
            this.ddlCurrency.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlCurrency.FormattingEnabled = true;
            this.ddlCurrency.Location = new System.Drawing.Point(140, 233);
            this.ddlCurrency.Margin = new System.Windows.Forms.Padding(2);
            this.ddlCurrency.Name = "ddlCurrency";
            this.ddlCurrency.Size = new System.Drawing.Size(197, 24);
            this.ddlCurrency.TabIndex = 17;
            // 
            // pnlExchange
            // 
            this.pnlExchange.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlExchange.Controls.Add(this.lblExchange);
            this.pnlExchange.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlExchange.Location = new System.Drawing.Point(2, 258);
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
            this.lblExchange.Location = new System.Drawing.Point(56, 2);
            this.lblExchange.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblExchange.Name = "lblExchange";
            this.lblExchange.Size = new System.Drawing.Size(74, 17);
            this.lblExchange.TabIndex = 0;
            this.lblExchange.Text = "Exchange:";
            // 
            // ddlExchange
            // 
            this.ddlExchange.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlExchange.Enabled = false;
            this.ddlExchange.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlExchange.FormattingEnabled = true;
            this.ddlExchange.Location = new System.Drawing.Point(140, 258);
            this.ddlExchange.Margin = new System.Windows.Forms.Padding(2);
            this.ddlExchange.Name = "ddlExchange";
            this.ddlExchange.Size = new System.Drawing.Size(197, 24);
            this.ddlExchange.TabIndex = 19;
            this.ddlExchange.SelectedIndexChanged += new System.EventHandler(this.ddlExchange_SelectedIndexChanged);
            // 
            // pnlMultiplier
            // 
            this.pnlMultiplier.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlMultiplier.Controls.Add(this.lblMultiplier);
            this.pnlMultiplier.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMultiplier.Location = new System.Drawing.Point(2, 284);
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
            this.lblMultiplier.Location = new System.Drawing.Point(60, 2);
            this.lblMultiplier.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblMultiplier.Name = "lblMultiplier";
            this.lblMultiplier.Size = new System.Drawing.Size(68, 17);
            this.lblMultiplier.TabIndex = 0;
            this.lblMultiplier.Text = "Multiplier:";
            // 
            // ddlMultiplier
            // 
            this.ddlMultiplier.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlMultiplier.Enabled = false;
            this.ddlMultiplier.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlMultiplier.FormattingEnabled = true;
            this.ddlMultiplier.Location = new System.Drawing.Point(140, 284);
            this.ddlMultiplier.Margin = new System.Windows.Forms.Padding(2);
            this.ddlMultiplier.Name = "ddlMultiplier";
            this.ddlMultiplier.Size = new System.Drawing.Size(197, 24);
            this.ddlMultiplier.TabIndex = 21;
            // 
            // FuturesOptionContractEditorControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.Controls.Add(this.pnlEditorSplitter);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "FuturesOptionContractEditorControl";
            this.Size = new System.Drawing.Size(943, 406);
            this.pnlEditorSplitter.Panel1.ResumeLayout(false);
            this.pnlEditorSplitter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pnlEditorSplitter)).EndInit();
            this.pnlEditorSplitter.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.futuresOptionContractViewModelBindingSource)).EndInit();
            this.pnlFuturesOptionContractIds.ResumeLayout(false);
            this.pnlFuturesOptionContractIds.PerformLayout();
            this.tlpFuturesOptionContract.ResumeLayout(false);
            this.tlpFuturesOptionContract.PerformLayout();
            this.pnlContractId.ResumeLayout(false);
            this.pnlContractId.PerformLayout();
            this.pnlDescription.ResumeLayout(false);
            this.pnlDescription.PerformLayout();
            this.pnlContractMonth.ResumeLayout(false);
            this.pnlContractMonth.PerformLayout();
            this.pnlStrikePrice.ResumeLayout(false);
            this.pnlStrikePrice.PerformLayout();
            this.pnlOptionType.ResumeLayout(false);
            this.pnlOptionType.PerformLayout();
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
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer pnlEditorSplitter;
        private System.Windows.Forms.TableLayoutPanel tlpFuturesOptionContract;
        private System.Windows.Forms.Panel pnlContractId;
        private System.Windows.Forms.Label label1;
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
