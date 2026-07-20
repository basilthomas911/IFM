namespace TomasAI.IFM.Views.Trade
{
    partial class TradeOrderEditorForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            pnlFundSelector = new Panel();
            btnCreateFund = new Button();
            ddlFund = new ComboBox();
            lblFundSelector = new Label();
            pnlTradeOrders = new Panel();
            lblTo = new Label();
            dtpTo = new DateTimePicker();
            lblFrom = new Label();
            dtpFrom = new DateTimePicker();
            btnCompleteOrder = new Button();
            btnLoadOrder = new Button();
            btnDeleteOrder = new Button();
            btnCreateOrder = new Button();
            lblTradeOrders = new Label();
            lstTradeOrders = new ListView();
            columnHeader13 = new ColumnHeader();
            columnHeader8 = new ColumnHeader();
            columnHeader9 = new ColumnHeader();
            columnHeader10 = new ColumnHeader();
            panel1 = new Panel();
            txtStatus = new TextBox();
            pnlTradePosition = new Panel();
            cbLiveFeed = new CheckBox();
            btnEndOfDay = new Button();
            txtTradeType = new TextBox();
            txtDaysToExpiry = new TextBox();
            lblDaysToExpiry = new Label();
            dtpTradeDate = new DateTimePicker();
            lblTradeDate = new Label();
            pnlTradeControl = new Panel();
            lblOrderAction = new Label();
            btnSubmitOrder = new Button();
            ddlOrderActionType = new ComboBox();
            lblTradeType = new Label();
            pnlTrades = new Panel();
            btnRemoveTrade = new Button();
            btnAddTrade = new Button();
            lstTrades = new ListView();
            colTradeId = new ColumnHeader();
            colTradeType = new ColumnHeader();
            colTradeDate = new ColumnHeader();
            colMaturityDate = new ColumnHeader();
            colTradeState = new ColumnHeader();
            colTradeAction = new ColumnHeader();
            label1 = new Label();
            btnOpenTrade = new Button();
            lblTrades = new Label();
            pnlFundSelector.SuspendLayout();
            pnlTradeOrders.SuspendLayout();
            panel1.SuspendLayout();
            pnlTradePosition.SuspendLayout();
            pnlTrades.SuspendLayout();
            SuspendLayout();
            // 
            // pnlFundSelector
            // 
            pnlFundSelector.BackColor = Color.FromArgb(64, 64, 64);
            pnlFundSelector.BorderStyle = BorderStyle.FixedSingle;
            pnlFundSelector.Controls.Add(btnCreateFund);
            pnlFundSelector.Controls.Add(ddlFund);
            pnlFundSelector.Controls.Add(lblFundSelector);
            pnlFundSelector.Dock = DockStyle.Top;
            pnlFundSelector.Location = new Point(0, 0);
            pnlFundSelector.Margin = new Padding(3, 2, 3, 2);
            pnlFundSelector.Name = "pnlFundSelector";
            pnlFundSelector.Size = new Size(1455, 57);
            pnlFundSelector.TabIndex = 0;
            // 
            // btnCreateFund
            // 
            btnCreateFund.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnCreateFund.ForeColor = Color.Black;
            btnCreateFund.Location = new Point(1260, 5);
            btnCreateFund.Margin = new Padding(3, 2, 3, 2);
            btnCreateFund.Name = "btnCreateFund";
            btnCreateFund.Size = new Size(174, 42);
            btnCreateFund.TabIndex = 2;
            btnCreateFund.Text = "Create Fund...";
            btnCreateFund.UseVisualStyleBackColor = true;
            btnCreateFund.Click += btnCreateFund_Click;
            // 
            // ddlFund
            // 
            ddlFund.BackColor = Color.FromArgb(64, 64, 64);
            ddlFund.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlFund.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlFund.ForeColor = Color.White;
            ddlFund.FormattingEnabled = true;
            ddlFund.Location = new Point(93, 9);
            ddlFund.Margin = new Padding(3, 2, 3, 2);
            ddlFund.Name = "ddlFund";
            ddlFund.Size = new Size(1151, 28);
            ddlFund.TabIndex = 1;
            ddlFund.SelectedIndexChanged += ddlFund_SelectedIndexChanged;
            // 
            // lblFundSelector
            // 
            lblFundSelector.AutoEllipsis = true;
            lblFundSelector.AutoSize = true;
            lblFundSelector.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblFundSelector.ForeColor = Color.White;
            lblFundSelector.Location = new Point(29, 16);
            lblFundSelector.Name = "lblFundSelector";
            lblFundSelector.Size = new Size(50, 20);
            lblFundSelector.TabIndex = 0;
            lblFundSelector.Text = "Fund:";
            lblFundSelector.TextAlign = ContentAlignment.MiddleRight;
            // 
            // pnlTradeOrders
            // 
            pnlTradeOrders.BackColor = Color.FromArgb(64, 64, 64);
            pnlTradeOrders.Controls.Add(lblTo);
            pnlTradeOrders.Controls.Add(dtpTo);
            pnlTradeOrders.Controls.Add(lblFrom);
            pnlTradeOrders.Controls.Add(dtpFrom);
            pnlTradeOrders.Controls.Add(btnCompleteOrder);
            pnlTradeOrders.Controls.Add(btnLoadOrder);
            pnlTradeOrders.Controls.Add(btnDeleteOrder);
            pnlTradeOrders.Controls.Add(btnCreateOrder);
            pnlTradeOrders.Controls.Add(lblTradeOrders);
            pnlTradeOrders.Controls.Add(lstTradeOrders);
            pnlTradeOrders.Dock = DockStyle.Top;
            pnlTradeOrders.Location = new Point(0, 57);
            pnlTradeOrders.Margin = new Padding(3, 2, 3, 2);
            pnlTradeOrders.Name = "pnlTradeOrders";
            pnlTradeOrders.Size = new Size(1455, 378);
            pnlTradeOrders.TabIndex = 1;
            // 
            // lblTo
            // 
            lblTo.AutoSize = true;
            lblTo.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTo.ForeColor = Color.White;
            lblTo.Location = new Point(519, 12);
            lblTo.Name = "lblTo";
            lblTo.Size = new Size(29, 17);
            lblTo.TabIndex = 9;
            lblTo.Text = "To:";
            // 
            // dtpTo
            // 
            dtpTo.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dtpTo.Location = new Point(555, 8);
            dtpTo.Margin = new Padding(3, 2, 3, 2);
            dtpTo.Name = "dtpTo";
            dtpTo.Size = new Size(378, 26);
            dtpTo.TabIndex = 8;
            dtpTo.ValueChanged += dtpTo_ValueChanged;
            // 
            // lblFrom
            // 
            lblFrom.AutoSize = true;
            lblFrom.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblFrom.ForeColor = Color.White;
            lblFrom.Location = new Point(30, 9);
            lblFrom.Name = "lblFrom";
            lblFrom.Size = new Size(50, 20);
            lblFrom.TabIndex = 7;
            lblFrom.Text = "From:";
            // 
            // dtpFrom
            // 
            dtpFrom.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dtpFrom.Location = new Point(94, 8);
            dtpFrom.Margin = new Padding(3, 2, 3, 2);
            dtpFrom.Name = "dtpFrom";
            dtpFrom.Size = new Size(386, 26);
            dtpFrom.TabIndex = 6;
            dtpFrom.ValueChanged += dtpFrom_ValueChanged;
            // 
            // btnCompleteOrder
            // 
            btnCompleteOrder.AutoSize = true;
            btnCompleteOrder.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnCompleteOrder.ForeColor = Color.Black;
            btnCompleteOrder.Location = new Point(1260, 199);
            btnCompleteOrder.Margin = new Padding(3, 2, 3, 2);
            btnCompleteOrder.Name = "btnCompleteOrder";
            btnCompleteOrder.Size = new Size(174, 42);
            btnCompleteOrder.TabIndex = 5;
            btnCompleteOrder.Text = "Close Order";
            btnCompleteOrder.UseVisualStyleBackColor = true;
            btnCompleteOrder.Click += btnCloseOrder_Click;
            // 
            // btnLoadOrder
            // 
            btnLoadOrder.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnLoadOrder.ForeColor = Color.Black;
            btnLoadOrder.Location = new Point(1260, 46);
            btnLoadOrder.Margin = new Padding(3, 2, 3, 2);
            btnLoadOrder.Name = "btnLoadOrder";
            btnLoadOrder.Size = new Size(174, 42);
            btnLoadOrder.TabIndex = 4;
            btnLoadOrder.Text = "Load Order";
            btnLoadOrder.UseVisualStyleBackColor = true;
            btnLoadOrder.Click += btnLoadOrder_Click;
            // 
            // btnDeleteOrder
            // 
            btnDeleteOrder.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnDeleteOrder.ForeColor = Color.Black;
            btnDeleteOrder.Location = new Point(1260, 154);
            btnDeleteOrder.Margin = new Padding(3, 2, 3, 2);
            btnDeleteOrder.Name = "btnDeleteOrder";
            btnDeleteOrder.Size = new Size(174, 40);
            btnDeleteOrder.TabIndex = 3;
            btnDeleteOrder.Text = "Delete Order...";
            btnDeleteOrder.UseVisualStyleBackColor = true;
            btnDeleteOrder.Click += btnCancelOrder_Click;
            // 
            // btnCreateOrder
            // 
            btnCreateOrder.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnCreateOrder.ForeColor = Color.Black;
            btnCreateOrder.Location = new Point(1260, 106);
            btnCreateOrder.Margin = new Padding(3, 2, 3, 2);
            btnCreateOrder.Name = "btnCreateOrder";
            btnCreateOrder.Size = new Size(174, 42);
            btnCreateOrder.TabIndex = 2;
            btnCreateOrder.Text = "Create Order...";
            btnCreateOrder.UseVisualStyleBackColor = true;
            btnCreateOrder.Click += btnCreateOrder_Click;
            // 
            // lblTradeOrders
            // 
            lblTradeOrders.AutoSize = true;
            lblTradeOrders.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTradeOrders.ForeColor = Color.White;
            lblTradeOrders.Location = new Point(19, 46);
            lblTradeOrders.Name = "lblTradeOrders";
            lblTradeOrders.Size = new Size(61, 20);
            lblTradeOrders.TabIndex = 1;
            lblTradeOrders.Text = "Orders:";
            lblTradeOrders.TextAlign = ContentAlignment.MiddleRight;
            // 
            // lstTradeOrders
            // 
            lstTradeOrders.BackColor = Color.Black;
            lstTradeOrders.Columns.AddRange(new ColumnHeader[] { columnHeader13, columnHeader8, columnHeader9, columnHeader10 });
            lstTradeOrders.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lstTradeOrders.ForeColor = Color.White;
            lstTradeOrders.FullRowSelect = true;
            lstTradeOrders.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lstTradeOrders.Location = new Point(94, 48);
            lstTradeOrders.Margin = new Padding(3, 2, 3, 2);
            lstTradeOrders.MultiSelect = false;
            lstTradeOrders.Name = "lstTradeOrders";
            lstTradeOrders.Size = new Size(1150, 324);
            lstTradeOrders.TabIndex = 0;
            lstTradeOrders.UseCompatibleStateImageBehavior = false;
            lstTradeOrders.View = View.Details;
            lstTradeOrders.SelectedIndexChanged += lstTradeOrders_SelectedIndexChanged;
            lstTradeOrders.DoubleClick += lstTradeOrders_DoubleClick;
            // 
            // columnHeader13
            // 
            columnHeader13.Text = "Order Id";
            columnHeader13.Width = 80;
            // 
            // columnHeader8
            // 
            columnHeader8.Text = "Order Date";
            columnHeader8.TextAlign = HorizontalAlignment.Center;
            columnHeader8.Width = 110;
            // 
            // columnHeader9
            // 
            columnHeader9.Text = "Order Status";
            columnHeader9.TextAlign = HorizontalAlignment.Center;
            columnHeader9.Width = 125;
            // 
            // columnHeader10
            // 
            columnHeader10.Text = "Reference";
            columnHeader10.Width = 800;
            // 
            // panel1
            // 
            panel1.Controls.Add(txtStatus);
            panel1.Controls.Add(pnlTradePosition);
            panel1.Controls.Add(pnlTrades);
            panel1.Controls.Add(btnOpenTrade);
            panel1.Controls.Add(lblTrades);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(0, 435);
            panel1.Margin = new Padding(3, 2, 3, 2);
            panel1.Name = "panel1";
            panel1.Size = new Size(1455, 729);
            panel1.TabIndex = 2;
            // 
            // txtStatus
            // 
            txtStatus.BackColor = Color.Black;
            txtStatus.BorderStyle = BorderStyle.FixedSingle;
            txtStatus.Dock = DockStyle.Bottom;
            txtStatus.Font = new Font("Microsoft Sans Serif", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtStatus.ForeColor = Color.White;
            txtStatus.Location = new Point(0, 700);
            txtStatus.Margin = new Padding(4, 5, 4, 5);
            txtStatus.Name = "txtStatus";
            txtStatus.Size = new Size(1455, 29);
            txtStatus.TabIndex = 6;
            // 
            // pnlTradePosition
            // 
            pnlTradePosition.BackColor = Color.FromArgb(64, 64, 64);
            pnlTradePosition.Controls.Add(cbLiveFeed);
            pnlTradePosition.Controls.Add(btnEndOfDay);
            pnlTradePosition.Controls.Add(txtTradeType);
            pnlTradePosition.Controls.Add(txtDaysToExpiry);
            pnlTradePosition.Controls.Add(lblDaysToExpiry);
            pnlTradePosition.Controls.Add(dtpTradeDate);
            pnlTradePosition.Controls.Add(lblTradeDate);
            pnlTradePosition.Controls.Add(pnlTradeControl);
            pnlTradePosition.Controls.Add(lblOrderAction);
            pnlTradePosition.Controls.Add(btnSubmitOrder);
            pnlTradePosition.Controls.Add(ddlOrderActionType);
            pnlTradePosition.Controls.Add(lblTradeType);
            pnlTradePosition.Dock = DockStyle.Fill;
            pnlTradePosition.Location = new Point(0, 222);
            pnlTradePosition.Margin = new Padding(3, 2, 3, 2);
            pnlTradePosition.Name = "pnlTradePosition";
            pnlTradePosition.Size = new Size(1455, 507);
            pnlTradePosition.TabIndex = 5;
            pnlTradePosition.Paint += pnlTradePosition_Paint;
            // 
            // cbLiveFeed
            // 
            cbLiveFeed.AutoSize = true;
            cbLiveFeed.BackColor = Color.FromArgb(64, 64, 64);
            cbLiveFeed.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            cbLiveFeed.ForeColor = Color.White;
            cbLiveFeed.Location = new Point(1261, 121);
            cbLiveFeed.Margin = new Padding(3, 4, 3, 4);
            cbLiveFeed.Name = "cbLiveFeed";
            cbLiveFeed.Size = new Size(97, 24);
            cbLiveFeed.TabIndex = 31;
            cbLiveFeed.Text = "Live Feed";
            cbLiveFeed.UseVisualStyleBackColor = false;
            cbLiveFeed.CheckedChanged += cbLiveFeed_CheckedChanged;
            // 
            // btnEndOfDay
            // 
            btnEndOfDay.AutoSize = true;
            btnEndOfDay.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnEndOfDay.ForeColor = Color.Black;
            btnEndOfDay.Location = new Point(1260, 60);
            btnEndOfDay.Margin = new Padding(3, 2, 3, 2);
            btnEndOfDay.Name = "btnEndOfDay";
            btnEndOfDay.Size = new Size(174, 42);
            btnEndOfDay.TabIndex = 6;
            btnEndOfDay.Text = "End Of Day...";
            btnEndOfDay.UseVisualStyleBackColor = true;
            btnEndOfDay.Click += btnEndOfDay_Click;
            // 
            // txtTradeType
            // 
            txtTradeType.BackColor = Color.Black;
            txtTradeType.BorderStyle = BorderStyle.FixedSingle;
            txtTradeType.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtTradeType.Location = new Point(94, 11);
            txtTradeType.Margin = new Padding(3, 2, 3, 2);
            txtTradeType.Name = "txtTradeType";
            txtTradeType.ReadOnly = true;
            txtTradeType.Size = new Size(287, 26);
            txtTradeType.TabIndex = 29;
            // 
            // txtDaysToExpiry
            // 
            txtDaysToExpiry.BackColor = Color.Black;
            txtDaysToExpiry.BorderStyle = BorderStyle.FixedSingle;
            txtDaysToExpiry.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtDaysToExpiry.Location = new Point(839, 11);
            txtDaysToExpiry.Margin = new Padding(3, 2, 3, 2);
            txtDaysToExpiry.Name = "txtDaysToExpiry";
            txtDaysToExpiry.ReadOnly = true;
            txtDaysToExpiry.Size = new Size(59, 26);
            txtDaysToExpiry.TabIndex = 28;
            txtDaysToExpiry.Visible = false;
            // 
            // lblDaysToExpiry
            // 
            lblDaysToExpiry.AutoSize = true;
            lblDaysToExpiry.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblDaysToExpiry.ForeColor = Color.White;
            lblDaysToExpiry.Location = new Point(716, 13);
            lblDaysToExpiry.Name = "lblDaysToExpiry";
            lblDaysToExpiry.Size = new Size(117, 20);
            lblDaysToExpiry.TabIndex = 27;
            lblDaysToExpiry.Text = "Days To Expiry:";
            lblDaysToExpiry.TextAlign = ContentAlignment.MiddleRight;
            lblDaysToExpiry.Visible = false;
            // 
            // dtpTradeDate
            // 
            dtpTradeDate.CalendarFont = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dtpTradeDate.CalendarMonthBackground = Color.White;
            dtpTradeDate.CustomFormat = "yyyy-MMM-dd";
            dtpTradeDate.Font = new Font("Microsoft Sans Serif", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dtpTradeDate.Format = DateTimePickerFormat.Custom;
            dtpTradeDate.Location = new Point(500, 11);
            dtpTradeDate.Margin = new Padding(3, 2, 3, 2);
            dtpTradeDate.Name = "dtpTradeDate";
            dtpTradeDate.Size = new Size(199, 24);
            dtpTradeDate.TabIndex = 26;
            dtpTradeDate.ValueChanged += dtpTradeDate_ValueChanged;
            // 
            // lblTradeDate
            // 
            lblTradeDate.AutoSize = true;
            lblTradeDate.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTradeDate.ForeColor = Color.White;
            lblTradeDate.Location = new Point(401, 13);
            lblTradeDate.Name = "lblTradeDate";
            lblTradeDate.Size = new Size(93, 20);
            lblTradeDate.TabIndex = 25;
            lblTradeDate.Text = "Trade Date:";
            lblTradeDate.TextAlign = ContentAlignment.MiddleRight;
            // 
            // pnlTradeControl
            // 
            pnlTradeControl.Location = new Point(94, 52);
            pnlTradeControl.Margin = new Padding(3, 2, 3, 2);
            pnlTradeControl.Name = "pnlTradeControl";
            pnlTradeControl.Size = new Size(1150, 419);
            pnlTradeControl.TabIndex = 24;
            // 
            // lblOrderAction
            // 
            lblOrderAction.AutoSize = true;
            lblOrderAction.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblOrderAction.ForeColor = Color.White;
            lblOrderAction.Location = new Point(982, 13);
            lblOrderAction.Name = "lblOrderAction";
            lblOrderAction.Size = new Size(102, 20);
            lblOrderAction.TabIndex = 22;
            lblOrderAction.Text = "Order Action:";
            lblOrderAction.TextAlign = ContentAlignment.MiddleRight;
            // 
            // btnSubmitOrder
            // 
            btnSubmitOrder.AutoSize = true;
            btnSubmitOrder.Enabled = false;
            btnSubmitOrder.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnSubmitOrder.ForeColor = Color.Black;
            btnSubmitOrder.Location = new Point(1260, 12);
            btnSubmitOrder.Margin = new Padding(3, 2, 3, 2);
            btnSubmitOrder.Name = "btnSubmitOrder";
            btnSubmitOrder.Size = new Size(174, 42);
            btnSubmitOrder.TabIndex = 20;
            btnSubmitOrder.Text = "Submit Order...";
            btnSubmitOrder.UseVisualStyleBackColor = true;
            btnSubmitOrder.Click += btnSubmitOrder_Click;
            // 
            // ddlOrderActionType
            // 
            ddlOrderActionType.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlOrderActionType.Enabled = false;
            ddlOrderActionType.Font = new Font("Microsoft Sans Serif", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlOrderActionType.FormattingEnabled = true;
            ddlOrderActionType.Location = new Point(1090, 11);
            ddlOrderActionType.Margin = new Padding(3, 2, 3, 2);
            ddlOrderActionType.Name = "ddlOrderActionType";
            ddlOrderActionType.Size = new Size(154, 26);
            ddlOrderActionType.TabIndex = 19;
            ddlOrderActionType.SelectedIndexChanged += ddlOrderActionType_SelectedIndexChanged;
            // 
            // lblTradeType
            // 
            lblTradeType.AutoSize = true;
            lblTradeType.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTradeType.ForeColor = Color.White;
            lblTradeType.Location = new Point(0, 13);
            lblTradeType.Margin = new Padding(0);
            lblTradeType.Name = "lblTradeType";
            lblTradeType.Size = new Size(92, 20);
            lblTradeType.TabIndex = 1;
            lblTradeType.Text = "Trade Type:";
            lblTradeType.TextAlign = ContentAlignment.MiddleRight;
            // 
            // pnlTrades
            // 
            pnlTrades.BackColor = Color.FromArgb(64, 64, 64);
            pnlTrades.Controls.Add(btnRemoveTrade);
            pnlTrades.Controls.Add(btnAddTrade);
            pnlTrades.Controls.Add(lstTrades);
            pnlTrades.Controls.Add(label1);
            pnlTrades.Dock = DockStyle.Top;
            pnlTrades.Location = new Point(0, 0);
            pnlTrades.Margin = new Padding(3, 2, 3, 2);
            pnlTrades.Name = "pnlTrades";
            pnlTrades.Size = new Size(1455, 222);
            pnlTrades.TabIndex = 4;
            // 
            // btnRemoveTrade
            // 
            btnRemoveTrade.AutoSize = true;
            btnRemoveTrade.Enabled = false;
            btnRemoveTrade.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnRemoveTrade.ForeColor = Color.Black;
            btnRemoveTrade.Location = new Point(1260, 58);
            btnRemoveTrade.Margin = new Padding(3, 2, 3, 2);
            btnRemoveTrade.Name = "btnRemoveTrade";
            btnRemoveTrade.Size = new Size(174, 42);
            btnRemoveTrade.TabIndex = 7;
            btnRemoveTrade.Text = "Remove Trade";
            btnRemoveTrade.UseVisualStyleBackColor = true;
            btnRemoveTrade.Click += btnRemoveTrade_Click;
            // 
            // btnAddTrade
            // 
            btnAddTrade.AutoSize = true;
            btnAddTrade.Enabled = false;
            btnAddTrade.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnAddTrade.ForeColor = Color.Black;
            btnAddTrade.Location = new Point(1260, 12);
            btnAddTrade.Margin = new Padding(3, 2, 3, 2);
            btnAddTrade.Name = "btnAddTrade";
            btnAddTrade.Size = new Size(174, 42);
            btnAddTrade.TabIndex = 6;
            btnAddTrade.Text = "Add Trade...";
            btnAddTrade.UseVisualStyleBackColor = true;
            btnAddTrade.Click += btnAddTrade_Click;
            // 
            // lstTrades
            // 
            lstTrades.BackColor = Color.Black;
            lstTrades.Columns.AddRange(new ColumnHeader[] { colTradeId, colTradeType, colTradeDate, colMaturityDate, colTradeState, colTradeAction });
            lstTrades.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lstTrades.ForeColor = Color.White;
            lstTrades.FullRowSelect = true;
            lstTrades.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lstTrades.Location = new Point(94, 8);
            lstTrades.Margin = new Padding(3, 2, 3, 2);
            lstTrades.MultiSelect = false;
            lstTrades.Name = "lstTrades";
            lstTrades.Size = new Size(1150, 205);
            lstTrades.TabIndex = 5;
            lstTrades.UseCompatibleStateImageBehavior = false;
            lstTrades.View = View.Details;
            lstTrades.SelectedIndexChanged += lstTrades_SelectedIndexChanged;
            lstTrades.Enter += lstTrades_Enter;
            lstTrades.Leave += lstTrades_Leave;
            // 
            // colTradeId
            // 
            colTradeId.Text = "Trade Id";
            colTradeId.Width = 80;
            // 
            // colTradeType
            // 
            colTradeType.Text = "Trade Type";
            colTradeType.Width = 110;
            // 
            // colTradeDate
            // 
            colTradeDate.Text = "Trade Date";
            colTradeDate.Width = 119;
            // 
            // colMaturityDate
            // 
            colMaturityDate.Text = "Maturity Date";
            colMaturityDate.Width = 120;
            // 
            // colTradeState
            // 
            colTradeState.Text = "Trade State";
            colTradeState.Width = 110;
            // 
            // colTradeAction
            // 
            colTradeAction.Text = "Trade Action";
            colTradeAction.Width = 387;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label1.ForeColor = Color.White;
            label1.Location = new Point(14, 12);
            label1.Name = "label1";
            label1.Size = new Size(62, 20);
            label1.TabIndex = 1;
            label1.Text = "Trades:";
            label1.TextAlign = ContentAlignment.MiddleRight;
            // 
            // btnOpenTrade
            // 
            btnOpenTrade.Location = new Point(1138, 8);
            btnOpenTrade.Margin = new Padding(3, 2, 3, 2);
            btnOpenTrade.Name = "btnOpenTrade";
            btnOpenTrade.Size = new Size(136, 32);
            btnOpenTrade.TabIndex = 3;
            btnOpenTrade.Text = "Open...";
            btnOpenTrade.UseVisualStyleBackColor = true;
            // 
            // lblTrades
            // 
            lblTrades.AutoSize = true;
            lblTrades.Location = new Point(26, 8);
            lblTrades.Name = "lblTrades";
            lblTrades.Size = new Size(62, 20);
            lblTrades.TabIndex = 1;
            lblTrades.Text = "Trades:";
            // 
            // TradeOrderEditorForm
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1455, 1164);
            Controls.Add(panel1);
            Controls.Add(pnlTradeOrders);
            Controls.Add(pnlFundSelector);
            Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ForeColor = Color.Black;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(3, 2, 3, 2);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "TradeOrderEditorForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Trade Orders";
            FormClosing += TradeOrderEditorForm_FormClosing;
            FormClosed += TradeOrderEditorForm_FormClosed;
            Load += TradeOrderForm_Load;
            Shown += TradeOrderEditorForm_Shown;
            pnlFundSelector.ResumeLayout(false);
            pnlFundSelector.PerformLayout();
            pnlTradeOrders.ResumeLayout(false);
            pnlTradeOrders.PerformLayout();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            pnlTradePosition.ResumeLayout(false);
            pnlTradePosition.PerformLayout();
            pnlTrades.ResumeLayout(false);
            pnlTrades.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlFundSelector;
        private System.Windows.Forms.Label lblFundSelector;
        private System.Windows.Forms.ComboBox ddlFund;
        private System.Windows.Forms.Panel pnlTradeOrders;
        private System.Windows.Forms.Button btnDeleteOrder;
        private System.Windows.Forms.Button btnCreateOrder;
        private System.Windows.Forms.Label lblTradeOrders;
        private System.Windows.Forms.ListView lstTradeOrders;
        private System.Windows.Forms.ColumnHeader columnHeader13;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel pnlTrades;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnOpenTrade;
        private System.Windows.Forms.Label lblTrades;
        private System.Windows.Forms.Button btnLoadOrder;
        private System.Windows.Forms.ColumnHeader columnHeader8;
        private System.Windows.Forms.ColumnHeader columnHeader9;
        private System.Windows.Forms.ColumnHeader columnHeader10;
        private System.Windows.Forms.ListView lstTrades;
        private System.Windows.Forms.ColumnHeader colTradeId;
        private System.Windows.Forms.ColumnHeader colTradeType;
        private System.Windows.Forms.ColumnHeader colTradeDate;
        private System.Windows.Forms.ColumnHeader colMaturityDate;
        private System.Windows.Forms.ColumnHeader colTradeState;
        private System.Windows.Forms.ColumnHeader colTradeAction;
        private System.Windows.Forms.Button btnCompleteOrder;
        private System.Windows.Forms.Panel pnlTradePosition;
        private System.Windows.Forms.Button btnSubmitOrder;
        private System.Windows.Forms.ComboBox ddlOrderActionType;
        private System.Windows.Forms.Label lblTradeType;
        private System.Windows.Forms.Label lblOrderAction;
        private System.Windows.Forms.Button btnAddTrade;
        private System.Windows.Forms.Button btnRemoveTrade;
        private System.Windows.Forms.Panel pnlTradeControl;
        private System.Windows.Forms.TextBox txtDaysToExpiry;
        private System.Windows.Forms.Label lblDaysToExpiry;
        private System.Windows.Forms.DateTimePicker dtpTradeDate;
        private System.Windows.Forms.Label lblTradeDate;
        private System.Windows.Forms.TextBox txtTradeType;
        private System.Windows.Forms.Button btnEndOfDay;
        private System.Windows.Forms.Button btnCreateFund;
        private System.Windows.Forms.Label lblTo;
        private System.Windows.Forms.DateTimePicker dtpTo;
        private System.Windows.Forms.Label lblFrom;
        private System.Windows.Forms.DateTimePicker dtpFrom;
        private System.Windows.Forms.TextBox txtStatus;
        private System.Windows.Forms.CheckBox cbLiveFeed;
    }
}