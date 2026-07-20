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
            this.pnlFundSelector = new System.Windows.Forms.Panel();
            this.btnCreateFund = new System.Windows.Forms.Button();
            this.ddlFund = new System.Windows.Forms.ComboBox();
            this.lblFundSelector = new System.Windows.Forms.Label();
            this.pnlTradeOrders = new System.Windows.Forms.Panel();
            this.lblTo = new System.Windows.Forms.Label();
            this.dtpTo = new System.Windows.Forms.DateTimePicker();
            this.lblFrom = new System.Windows.Forms.Label();
            this.dtpFrom = new System.Windows.Forms.DateTimePicker();
            this.btnCompleteOrder = new System.Windows.Forms.Button();
            this.btnLoadOrder = new System.Windows.Forms.Button();
            this.btnDeleteOrder = new System.Windows.Forms.Button();
            this.btnCreateOrder = new System.Windows.Forms.Button();
            this.lblTradeOrders = new System.Windows.Forms.Label();
            this.lstTradeOrders = new System.Windows.Forms.ListView();
            this.columnHeader13 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader8 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader9 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader10 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panel1 = new System.Windows.Forms.Panel();
            this.txtStatus = new System.Windows.Forms.TextBox();
            this.pnlTradePosition = new System.Windows.Forms.Panel();
            this.cbLiveFeed = new System.Windows.Forms.CheckBox();
            this.btnEndOfDay = new System.Windows.Forms.Button();
            this.txtTradeType = new System.Windows.Forms.TextBox();
            this.txtDaysToExpiry = new System.Windows.Forms.TextBox();
            this.lblDaysToExpiry = new System.Windows.Forms.Label();
            this.dtpTradeDate = new System.Windows.Forms.DateTimePicker();
            this.lblTradeDate = new System.Windows.Forms.Label();
            this.pnlTradeControl = new System.Windows.Forms.Panel();
            this.lblOrderAction = new System.Windows.Forms.Label();
            this.btnSubmitOrder = new System.Windows.Forms.Button();
            this.ddlOrderActionType = new System.Windows.Forms.ComboBox();
            this.lblTradeType = new System.Windows.Forms.Label();
            this.pnlTrades = new System.Windows.Forms.Panel();
            this.btnRemoveTrade = new System.Windows.Forms.Button();
            this.btnAddTrade = new System.Windows.Forms.Button();
            this.lstTrades = new System.Windows.Forms.ListView();
            this.colTradeId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTradeType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTradeDate = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colMaturityDate = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTradeState = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colTradeAction = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.label1 = new System.Windows.Forms.Label();
            this.btnOpenTrade = new System.Windows.Forms.Button();
            this.lblTrades = new System.Windows.Forms.Label();
            this.pnlFundSelector.SuspendLayout();
            this.pnlTradeOrders.SuspendLayout();
            this.panel1.SuspendLayout();
            this.pnlTradePosition.SuspendLayout();
            this.pnlTrades.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlFundSelector
            // 
            this.pnlFundSelector.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlFundSelector.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlFundSelector.Controls.Add(this.btnCreateFund);
            this.pnlFundSelector.Controls.Add(this.ddlFund);
            this.pnlFundSelector.Controls.Add(this.lblFundSelector);
            this.pnlFundSelector.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlFundSelector.Location = new System.Drawing.Point(0, 0);
            this.pnlFundSelector.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.pnlFundSelector.Name = "pnlFundSelector";
            this.pnlFundSelector.Size = new System.Drawing.Size(1293, 46);
            this.pnlFundSelector.TabIndex = 0;
            // 
            // btnCreateFund
            // 
            this.btnCreateFund.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCreateFund.Location = new System.Drawing.Point(1120, 4);
            this.btnCreateFund.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnCreateFund.Name = "btnCreateFund";
            this.btnCreateFund.Size = new System.Drawing.Size(155, 34);
            this.btnCreateFund.TabIndex = 2;
            this.btnCreateFund.Text = "Create Fund...";
            this.btnCreateFund.UseVisualStyleBackColor = true;
            this.btnCreateFund.Click += new System.EventHandler(this.btnCreateFund_Click);
            // 
            // ddlFund
            // 
            this.ddlFund.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.ddlFund.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlFund.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlFund.ForeColor = System.Drawing.Color.White;
            this.ddlFund.FormattingEnabled = true;
            this.ddlFund.Location = new System.Drawing.Point(91, 7);
            this.ddlFund.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.ddlFund.Name = "ddlFund";
            this.ddlFund.Size = new System.Drawing.Size(1016, 28);
            this.ddlFund.TabIndex = 1;
            this.ddlFund.SelectedIndexChanged += new System.EventHandler(this.ddlFund_SelectedIndexChanged);
            // 
            // lblFundSelector
            // 
            this.lblFundSelector.AutoEllipsis = true;
            this.lblFundSelector.AutoSize = true;
            this.lblFundSelector.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFundSelector.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblFundSelector.Location = new System.Drawing.Point(26, 13);
            this.lblFundSelector.Name = "lblFundSelector";
            this.lblFundSelector.Size = new System.Drawing.Size(44, 17);
            this.lblFundSelector.TabIndex = 0;
            this.lblFundSelector.Text = "Fund:";
            this.lblFundSelector.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // pnlTradeOrders
            // 
            this.pnlTradeOrders.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlTradeOrders.Controls.Add(this.lblTo);
            this.pnlTradeOrders.Controls.Add(this.dtpTo);
            this.pnlTradeOrders.Controls.Add(this.lblFrom);
            this.pnlTradeOrders.Controls.Add(this.dtpFrom);
            this.pnlTradeOrders.Controls.Add(this.btnCompleteOrder);
            this.pnlTradeOrders.Controls.Add(this.btnLoadOrder);
            this.pnlTradeOrders.Controls.Add(this.btnDeleteOrder);
            this.pnlTradeOrders.Controls.Add(this.btnCreateOrder);
            this.pnlTradeOrders.Controls.Add(this.lblTradeOrders);
            this.pnlTradeOrders.Controls.Add(this.lstTradeOrders);
            this.pnlTradeOrders.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTradeOrders.Location = new System.Drawing.Point(0, 46);
            this.pnlTradeOrders.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.pnlTradeOrders.Name = "pnlTradeOrders";
            this.pnlTradeOrders.Size = new System.Drawing.Size(1293, 302);
            this.pnlTradeOrders.TabIndex = 1;
            // 
            // lblTo
            // 
            this.lblTo.AutoSize = true;
            this.lblTo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTo.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblTo.Location = new System.Drawing.Point(455, 7);
            this.lblTo.Name = "lblTo";
            this.lblTo.Size = new System.Drawing.Size(29, 17);
            this.lblTo.TabIndex = 9;
            this.lblTo.Text = "To:";
            // 
            // dtpTo
            // 
            this.dtpTo.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtpTo.Location = new System.Drawing.Point(493, 6);
            this.dtpTo.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.dtpTo.Name = "dtpTo";
            this.dtpTo.Size = new System.Drawing.Size(336, 26);
            this.dtpTo.TabIndex = 8;
            this.dtpTo.ValueChanged += new System.EventHandler(this.dtpTo_ValueChanged);
            // 
            // lblFrom
            // 
            this.lblFrom.AutoSize = true;
            this.lblFrom.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFrom.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblFrom.Location = new System.Drawing.Point(27, 7);
            this.lblFrom.Name = "lblFrom";
            this.lblFrom.Size = new System.Drawing.Size(44, 17);
            this.lblFrom.TabIndex = 7;
            this.lblFrom.Text = "From:";
            // 
            // dtpFrom
            // 
            this.dtpFrom.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtpFrom.Location = new System.Drawing.Point(91, 6);
            this.dtpFrom.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.dtpFrom.Name = "dtpFrom";
            this.dtpFrom.Size = new System.Drawing.Size(336, 26);
            this.dtpFrom.TabIndex = 6;
            this.dtpFrom.ValueChanged += new System.EventHandler(this.dtpFrom_ValueChanged);
            // 
            // btnCompleteOrder
            // 
            this.btnCompleteOrder.AutoSize = true;
            this.btnCompleteOrder.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCompleteOrder.Location = new System.Drawing.Point(1120, 159);
            this.btnCompleteOrder.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnCompleteOrder.Name = "btnCompleteOrder";
            this.btnCompleteOrder.Size = new System.Drawing.Size(155, 34);
            this.btnCompleteOrder.TabIndex = 5;
            this.btnCompleteOrder.Text = "Close Order";
            this.btnCompleteOrder.UseVisualStyleBackColor = true;
            this.btnCompleteOrder.Click += new System.EventHandler(this.btnCloseOrder_Click);
            // 
            // btnLoadOrder
            // 
            this.btnLoadOrder.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnLoadOrder.Location = new System.Drawing.Point(1120, 37);
            this.btnLoadOrder.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnLoadOrder.Name = "btnLoadOrder";
            this.btnLoadOrder.Size = new System.Drawing.Size(155, 34);
            this.btnLoadOrder.TabIndex = 4;
            this.btnLoadOrder.Text = "Load Order";
            this.btnLoadOrder.UseVisualStyleBackColor = true;
            this.btnLoadOrder.Click += new System.EventHandler(this.btnLoadOrder_Click);
            // 
            // btnDeleteOrder
            // 
            this.btnDeleteOrder.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnDeleteOrder.Location = new System.Drawing.Point(1120, 123);
            this.btnDeleteOrder.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnDeleteOrder.Name = "btnDeleteOrder";
            this.btnDeleteOrder.Size = new System.Drawing.Size(155, 32);
            this.btnDeleteOrder.TabIndex = 3;
            this.btnDeleteOrder.Text = "Delete Order...";
            this.btnDeleteOrder.UseVisualStyleBackColor = true;
            this.btnDeleteOrder.Click += new System.EventHandler(this.btnCancelOrder_Click);
            // 
            // btnCreateOrder
            // 
            this.btnCreateOrder.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCreateOrder.Location = new System.Drawing.Point(1120, 85);
            this.btnCreateOrder.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnCreateOrder.Name = "btnCreateOrder";
            this.btnCreateOrder.Size = new System.Drawing.Size(155, 34);
            this.btnCreateOrder.TabIndex = 2;
            this.btnCreateOrder.Text = "Create Order...";
            this.btnCreateOrder.UseVisualStyleBackColor = true;
            this.btnCreateOrder.Click += new System.EventHandler(this.btnCreateOrder_Click);
            // 
            // lblTradeOrders
            // 
            this.lblTradeOrders.AutoSize = true;
            this.lblTradeOrders.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTradeOrders.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblTradeOrders.Location = new System.Drawing.Point(13, 38);
            this.lblTradeOrders.Name = "lblTradeOrders";
            this.lblTradeOrders.Size = new System.Drawing.Size(56, 17);
            this.lblTradeOrders.TabIndex = 1;
            this.lblTradeOrders.Text = "Orders:";
            this.lblTradeOrders.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lstTradeOrders
            // 
            this.lstTradeOrders.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lstTradeOrders.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader13,
            this.columnHeader8,
            this.columnHeader9,
            this.columnHeader10});
            this.lstTradeOrders.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstTradeOrders.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lstTradeOrders.FullRowSelect = true;
            this.lstTradeOrders.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lstTradeOrders.HideSelection = false;
            this.lstTradeOrders.Location = new System.Drawing.Point(91, 38);
            this.lstTradeOrders.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.lstTradeOrders.MultiSelect = false;
            this.lstTradeOrders.Name = "lstTradeOrders";
            this.lstTradeOrders.Size = new System.Drawing.Size(1016, 260);
            this.lstTradeOrders.TabIndex = 0;
            this.lstTradeOrders.UseCompatibleStateImageBehavior = false;
            this.lstTradeOrders.View = System.Windows.Forms.View.Details;
            this.lstTradeOrders.SelectedIndexChanged += new System.EventHandler(this.lstTradeOrders_SelectedIndexChanged);
            this.lstTradeOrders.DoubleClick += new System.EventHandler(this.lstTradeOrders_DoubleClick);
            // 
            // columnHeader13
            // 
            this.columnHeader13.Text = "Order Id";
            this.columnHeader13.Width = 80;
            // 
            // columnHeader8
            // 
            this.columnHeader8.Text = "Order Date";
            this.columnHeader8.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.columnHeader8.Width = 110;
            // 
            // columnHeader9
            // 
            this.columnHeader9.Text = "Order Status";
            this.columnHeader9.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.columnHeader9.Width = 125;
            // 
            // columnHeader10
            // 
            this.columnHeader10.Text = "Reference";
            this.columnHeader10.Width = 800;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.txtStatus);
            this.panel1.Controls.Add(this.pnlTradePosition);
            this.panel1.Controls.Add(this.pnlTrades);
            this.panel1.Controls.Add(this.btnOpenTrade);
            this.panel1.Controls.Add(this.lblTrades);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 348);
            this.panel1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1293, 583);
            this.panel1.TabIndex = 2;
            // 
            // txtStatus
            // 
            this.txtStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtStatus.Location = new System.Drawing.Point(0, 554);
            this.txtStatus.Margin = new System.Windows.Forms.Padding(4);
            this.txtStatus.Name = "txtStatus";
            this.txtStatus.Size = new System.Drawing.Size(1293, 29);
            this.txtStatus.TabIndex = 6;
            // 
            // pnlTradePosition
            // 
            this.pnlTradePosition.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlTradePosition.Controls.Add(this.cbLiveFeed);
            this.pnlTradePosition.Controls.Add(this.btnEndOfDay);
            this.pnlTradePosition.Controls.Add(this.txtTradeType);
            this.pnlTradePosition.Controls.Add(this.txtDaysToExpiry);
            this.pnlTradePosition.Controls.Add(this.lblDaysToExpiry);
            this.pnlTradePosition.Controls.Add(this.dtpTradeDate);
            this.pnlTradePosition.Controls.Add(this.lblTradeDate);
            this.pnlTradePosition.Controls.Add(this.pnlTradeControl);
            this.pnlTradePosition.Controls.Add(this.lblOrderAction);
            this.pnlTradePosition.Controls.Add(this.btnSubmitOrder);
            this.pnlTradePosition.Controls.Add(this.ddlOrderActionType);
            this.pnlTradePosition.Controls.Add(this.lblTradeType);
            this.pnlTradePosition.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlTradePosition.Location = new System.Drawing.Point(0, 178);
            this.pnlTradePosition.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.pnlTradePosition.Name = "pnlTradePosition";
            this.pnlTradePosition.Size = new System.Drawing.Size(1293, 405);
            this.pnlTradePosition.TabIndex = 5;
            this.pnlTradePosition.Paint += new System.Windows.Forms.PaintEventHandler(this.pnlTradePosition_Paint);
            // 
            // cbLiveFeed
            // 
            this.cbLiveFeed.AutoSize = true;
            this.cbLiveFeed.BackColor = System.Drawing.Color.DarkGray;
            this.cbLiveFeed.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cbLiveFeed.ForeColor = System.Drawing.Color.Black;
            this.cbLiveFeed.Location = new System.Drawing.Point(1121, 97);
            this.cbLiveFeed.Name = "cbLiveFeed";
            this.cbLiveFeed.Size = new System.Drawing.Size(97, 24);
            this.cbLiveFeed.TabIndex = 31;
            this.cbLiveFeed.Text = "Live Feed";
            this.cbLiveFeed.UseVisualStyleBackColor = false;
            this.cbLiveFeed.CheckedChanged += new System.EventHandler(this.cbLiveFeed_CheckedChanged);
            // 
            // btnEndOfDay
            // 
            this.btnEndOfDay.AutoSize = true;
            this.btnEndOfDay.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnEndOfDay.Location = new System.Drawing.Point(1120, 48);
            this.btnEndOfDay.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnEndOfDay.Name = "btnEndOfDay";
            this.btnEndOfDay.Size = new System.Drawing.Size(155, 34);
            this.btnEndOfDay.TabIndex = 6;
            this.btnEndOfDay.Text = "End Of Day...";
            this.btnEndOfDay.UseVisualStyleBackColor = true;
            this.btnEndOfDay.Click += new System.EventHandler(this.btnEndOfDay_Click);
            // 
            // txtTradeType
            // 
            this.txtTradeType.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtTradeType.Location = new System.Drawing.Point(116, 9);
            this.txtTradeType.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txtTradeType.Name = "txtTradeType";
            this.txtTradeType.ReadOnly = true;
            this.txtTradeType.Size = new System.Drawing.Size(221, 24);
            this.txtTradeType.TabIndex = 29;
            // 
            // txtDaysToExpiry
            // 
            this.txtDaysToExpiry.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDaysToExpiry.Location = new System.Drawing.Point(787, 10);
            this.txtDaysToExpiry.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txtDaysToExpiry.Name = "txtDaysToExpiry";
            this.txtDaysToExpiry.ReadOnly = true;
            this.txtDaysToExpiry.Size = new System.Drawing.Size(53, 24);
            this.txtDaysToExpiry.TabIndex = 28;
            this.txtDaysToExpiry.Visible = false;
            // 
            // lblDaysToExpiry
            // 
            this.lblDaysToExpiry.AutoSize = true;
            this.lblDaysToExpiry.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDaysToExpiry.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblDaysToExpiry.Location = new System.Drawing.Point(643, 12);
            this.lblDaysToExpiry.Name = "lblDaysToExpiry";
            this.lblDaysToExpiry.Size = new System.Drawing.Size(107, 17);
            this.lblDaysToExpiry.TabIndex = 27;
            this.lblDaysToExpiry.Text = "Days To Expiry:";
            this.lblDaysToExpiry.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblDaysToExpiry.Visible = false;
            // 
            // dtpTradeDate
            // 
            this.dtpTradeDate.CustomFormat = "yyyy-MMM-dd";
            this.dtpTradeDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtpTradeDate.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpTradeDate.Location = new System.Drawing.Point(459, 10);
            this.dtpTradeDate.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.dtpTradeDate.Name = "dtpTradeDate";
            this.dtpTradeDate.Size = new System.Drawing.Size(177, 24);
            this.dtpTradeDate.TabIndex = 26;
            this.dtpTradeDate.ValueChanged += new System.EventHandler(this.dtpTradeDate_ValueChanged);
            // 
            // lblTradeDate
            // 
            this.lblTradeDate.AutoSize = true;
            this.lblTradeDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTradeDate.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblTradeDate.Location = new System.Drawing.Point(343, 14);
            this.lblTradeDate.Name = "lblTradeDate";
            this.lblTradeDate.Size = new System.Drawing.Size(84, 17);
            this.lblTradeDate.TabIndex = 25;
            this.lblTradeDate.Text = "Trade Date:";
            this.lblTradeDate.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // pnlTradeControl
            // 
            this.pnlTradeControl.Location = new System.Drawing.Point(81, 50);
            this.pnlTradeControl.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.pnlTradeControl.Name = "pnlTradeControl";
            this.pnlTradeControl.Size = new System.Drawing.Size(1025, 321);
            this.pnlTradeControl.TabIndex = 24;
            // 
            // lblOrderAction
            // 
            this.lblOrderAction.AutoSize = true;
            this.lblOrderAction.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblOrderAction.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblOrderAction.Location = new System.Drawing.Point(845, 14);
            this.lblOrderAction.Name = "lblOrderAction";
            this.lblOrderAction.Size = new System.Drawing.Size(92, 17);
            this.lblOrderAction.TabIndex = 22;
            this.lblOrderAction.Text = "Order Action:";
            this.lblOrderAction.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // btnSubmitOrder
            // 
            this.btnSubmitOrder.AutoSize = true;
            this.btnSubmitOrder.Enabled = false;
            this.btnSubmitOrder.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSubmitOrder.Location = new System.Drawing.Point(1120, 10);
            this.btnSubmitOrder.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnSubmitOrder.Name = "btnSubmitOrder";
            this.btnSubmitOrder.Size = new System.Drawing.Size(155, 34);
            this.btnSubmitOrder.TabIndex = 20;
            this.btnSubmitOrder.Text = "Submit Order...";
            this.btnSubmitOrder.UseVisualStyleBackColor = true;
            this.btnSubmitOrder.Click += new System.EventHandler(this.btnSubmitOrder_Click);
            // 
            // ddlOrderActionType
            // 
            this.ddlOrderActionType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlOrderActionType.Enabled = false;
            this.ddlOrderActionType.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlOrderActionType.FormattingEnabled = true;
            this.ddlOrderActionType.Location = new System.Drawing.Point(969, 10);
            this.ddlOrderActionType.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.ddlOrderActionType.Name = "ddlOrderActionType";
            this.ddlOrderActionType.Size = new System.Drawing.Size(137, 26);
            this.ddlOrderActionType.TabIndex = 19;
            this.ddlOrderActionType.SelectedIndexChanged += new System.EventHandler(this.ddlOrderActionType_SelectedIndexChanged);
            // 
            // lblTradeType
            // 
            this.lblTradeType.AutoSize = true;
            this.lblTradeType.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTradeType.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblTradeType.Location = new System.Drawing.Point(3, 14);
            this.lblTradeType.Name = "lblTradeType";
            this.lblTradeType.Size = new System.Drawing.Size(86, 17);
            this.lblTradeType.TabIndex = 1;
            this.lblTradeType.Text = "Trade Type:";
            this.lblTradeType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // pnlTrades
            // 
            this.pnlTrades.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlTrades.Controls.Add(this.btnRemoveTrade);
            this.pnlTrades.Controls.Add(this.btnAddTrade);
            this.pnlTrades.Controls.Add(this.lstTrades);
            this.pnlTrades.Controls.Add(this.label1);
            this.pnlTrades.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTrades.Location = new System.Drawing.Point(0, 0);
            this.pnlTrades.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.pnlTrades.Name = "pnlTrades";
            this.pnlTrades.Size = new System.Drawing.Size(1293, 178);
            this.pnlTrades.TabIndex = 4;
            // 
            // btnRemoveTrade
            // 
            this.btnRemoveTrade.AutoSize = true;
            this.btnRemoveTrade.Enabled = false;
            this.btnRemoveTrade.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRemoveTrade.Location = new System.Drawing.Point(1120, 46);
            this.btnRemoveTrade.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnRemoveTrade.Name = "btnRemoveTrade";
            this.btnRemoveTrade.Size = new System.Drawing.Size(155, 34);
            this.btnRemoveTrade.TabIndex = 7;
            this.btnRemoveTrade.Text = "Remove Trade";
            this.btnRemoveTrade.UseVisualStyleBackColor = true;
            this.btnRemoveTrade.Click += new System.EventHandler(this.btnRemoveTrade_Click);
            // 
            // btnAddTrade
            // 
            this.btnAddTrade.AutoSize = true;
            this.btnAddTrade.Enabled = false;
            this.btnAddTrade.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAddTrade.Location = new System.Drawing.Point(1120, 10);
            this.btnAddTrade.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnAddTrade.Name = "btnAddTrade";
            this.btnAddTrade.Size = new System.Drawing.Size(155, 34);
            this.btnAddTrade.TabIndex = 6;
            this.btnAddTrade.Text = "Add Trade...";
            this.btnAddTrade.UseVisualStyleBackColor = true;
            this.btnAddTrade.Click += new System.EventHandler(this.btnAddTrade_Click);
            // 
            // lstTrades
            // 
            this.lstTrades.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lstTrades.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colTradeId,
            this.colTradeType,
            this.colTradeDate,
            this.colMaturityDate,
            this.colTradeState,
            this.colTradeAction});
            this.lstTrades.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstTrades.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lstTrades.FullRowSelect = true;
            this.lstTrades.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lstTrades.HideSelection = false;
            this.lstTrades.Location = new System.Drawing.Point(91, 6);
            this.lstTrades.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.lstTrades.MultiSelect = false;
            this.lstTrades.Name = "lstTrades";
            this.lstTrades.Size = new System.Drawing.Size(1016, 165);
            this.lstTrades.TabIndex = 5;
            this.lstTrades.UseCompatibleStateImageBehavior = false;
            this.lstTrades.View = System.Windows.Forms.View.Details;
            this.lstTrades.SelectedIndexChanged += new System.EventHandler(this.lstTrades_SelectedIndexChanged);
            this.lstTrades.Enter += new System.EventHandler(this.lstTrades_Enter);
            this.lstTrades.Leave += new System.EventHandler(this.lstTrades_Leave);
            // 
            // colTradeId
            // 
            this.colTradeId.Text = "Trade Id";
            this.colTradeId.Width = 80;
            // 
            // colTradeType
            // 
            this.colTradeType.Text = "Trade Type";
            this.colTradeType.Width = 110;
            // 
            // colTradeDate
            // 
            this.colTradeDate.Text = "Trade Date";
            this.colTradeDate.Width = 119;
            // 
            // colMaturityDate
            // 
            this.colMaturityDate.Text = "Maturity Date";
            this.colMaturityDate.Width = 120;
            // 
            // colTradeState
            // 
            this.colTradeState.Text = "Trade State";
            this.colTradeState.Width = 110;
            // 
            // colTradeAction
            // 
            this.colTradeAction.Text = "Trade Action";
            this.colTradeAction.Width = 387;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.label1.Location = new System.Drawing.Point(12, 10);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(57, 17);
            this.label1.TabIndex = 1;
            this.label1.Text = "Trades:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // btnOpenTrade
            // 
            this.btnOpenTrade.Location = new System.Drawing.Point(1012, 6);
            this.btnOpenTrade.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnOpenTrade.Name = "btnOpenTrade";
            this.btnOpenTrade.Size = new System.Drawing.Size(121, 26);
            this.btnOpenTrade.TabIndex = 3;
            this.btnOpenTrade.Text = "Open...";
            this.btnOpenTrade.UseVisualStyleBackColor = true;
            // 
            // lblTrades
            // 
            this.lblTrades.AutoSize = true;
            this.lblTrades.Location = new System.Drawing.Point(23, 6);
            this.lblTrades.Name = "lblTrades";
            this.lblTrades.Size = new System.Drawing.Size(54, 16);
            this.lblTrades.TabIndex = 1;
            this.lblTrades.Text = "Trades:";
            // 
            // TradeOrderEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1293, 931);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.pnlTradeOrders);
            this.Controls.Add(this.pnlFundSelector);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TradeOrderEditorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Trade Orders";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TradeOrderEditorForm_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.TradeOrderEditorForm_FormClosed);
            this.Load += new System.EventHandler(this.TradeOrderForm_Load);
            this.Shown += new System.EventHandler(this.TradeOrderEditorForm_Shown);
            this.pnlFundSelector.ResumeLayout(false);
            this.pnlFundSelector.PerformLayout();
            this.pnlTradeOrders.ResumeLayout(false);
            this.pnlTradeOrders.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.pnlTradePosition.ResumeLayout(false);
            this.pnlTradePosition.PerformLayout();
            this.pnlTrades.ResumeLayout(false);
            this.pnlTrades.PerformLayout();
            this.ResumeLayout(false);

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