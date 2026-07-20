namespace TomasAI.IFM.Views.Fund
{
    partial class FundTransactionEditor
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FundTransactionEditor));
            this.pnlFundSelector = new System.Windows.Forms.Panel();
            this.lblFundBalance = new System.Windows.Forms.Label();
            this.txtFundBalance = new System.Windows.Forms.TextBox();
            this.lblTo = new System.Windows.Forms.Label();
            this.dtpTo = new System.Windows.Forms.DateTimePicker();
            this.lblFrom = new System.Windows.Forms.Label();
            this.dtpFrom = new System.Windows.Forms.DateTimePicker();
            this.ddlFund = new System.Windows.Forms.ComboBox();
            this.lblFundSelector = new System.Windows.Forms.Label();
            this.pnlTradeOrders = new System.Windows.Forms.Panel();
            this.lblMetrics = new System.Windows.Forms.Label();
            this.tlpFundMetrics = new System.Windows.Forms.TableLayoutPanel();
            this.pnlWinRate = new System.Windows.Forms.Panel();
            this.lblWinRate = new System.Windows.Forms.Label();
            this.txtWinRate = new System.Windows.Forms.TextBox();
            this.pnlAverageProfit = new System.Windows.Forms.Panel();
            this.lblAverageProfit = new System.Windows.Forms.Label();
            this.txtAverageProfit = new System.Windows.Forms.TextBox();
            this.pnlAverageLoss = new System.Windows.Forms.Panel();
            this.lblLossRate = new System.Windows.Forms.Label();
            this.txtLossRate = new System.Windows.Forms.TextBox();
            this.txtAverageLoss = new System.Windows.Forms.TextBox();
            this.pnlProfitLoss = new System.Windows.Forms.Panel();
            this.lblProfitLoss = new System.Windows.Forms.Label();
            this.pnlProfitLossPercent = new System.Windows.Forms.Panel();
            this.lblProfitLossPercent = new System.Windows.Forms.Label();
            this.txtProfitLoss = new System.Windows.Forms.TextBox();
            this.txtProfitLossPercent = new System.Windows.Forms.TextBox();
            this.txtCommission = new System.Windows.Forms.TextBox();
            this.lblComment = new System.Windows.Forms.Label();
            this.txtComment = new System.Windows.Forms.TextBox();
            this.gridTransactions = new System.Windows.Forms.DataGridView();
            this.transactionIdDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.transactionDateDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.transactionTypeDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.orderIdDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tradeIdDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tradeTypeDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.valueDateDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tradeStatusDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.amountDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.balanceDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.fundTransactionBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.btnAdjust = new System.Windows.Forms.Button();
            this.lblTradeOrders = new System.Windows.Forms.Label();
            this.lblCommission = new System.Windows.Forms.Label();
            this.pnlCommission = new System.Windows.Forms.Panel();
            this.lblAverageLoss = new System.Windows.Forms.Label();
            this.pnlSharpeRatio = new System.Windows.Forms.Panel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.txtSharpeRatio = new System.Windows.Forms.TextBox();
            this.lblSharpeRatio = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.lblWinLossRatio = new System.Windows.Forms.Label();
            this.txtWinLossRatio = new System.Windows.Forms.TextBox();
            this.pnlFundSelector.SuspendLayout();
            this.pnlTradeOrders.SuspendLayout();
            this.tlpFundMetrics.SuspendLayout();
            this.pnlWinRate.SuspendLayout();
            this.pnlAverageProfit.SuspendLayout();
            this.pnlAverageLoss.SuspendLayout();
            this.pnlProfitLoss.SuspendLayout();
            this.pnlProfitLossPercent.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridTransactions)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.fundTransactionBindingSource)).BeginInit();
            this.pnlCommission.SuspendLayout();
            this.pnlSharpeRatio.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlFundSelector
            // 
            this.pnlFundSelector.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlFundSelector.Controls.Add(this.lblFundBalance);
            this.pnlFundSelector.Controls.Add(this.txtFundBalance);
            this.pnlFundSelector.Controls.Add(this.lblTo);
            this.pnlFundSelector.Controls.Add(this.dtpTo);
            this.pnlFundSelector.Controls.Add(this.lblFrom);
            this.pnlFundSelector.Controls.Add(this.dtpFrom);
            this.pnlFundSelector.Controls.Add(this.ddlFund);
            this.pnlFundSelector.Controls.Add(this.lblFundSelector);
            this.pnlFundSelector.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlFundSelector.Location = new System.Drawing.Point(0, 0);
            this.pnlFundSelector.Margin = new System.Windows.Forms.Padding(2);
            this.pnlFundSelector.Name = "pnlFundSelector";
            this.pnlFundSelector.Size = new System.Drawing.Size(1235, 67);
            this.pnlFundSelector.TabIndex = 1;
            // 
            // lblFundBalance
            // 
            this.lblFundBalance.AutoSize = true;
            this.lblFundBalance.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFundBalance.ForeColor = System.Drawing.Color.White;
            this.lblFundBalance.Location = new System.Drawing.Point(905, 38);
            this.lblFundBalance.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblFundBalance.Name = "lblFundBalance";
            this.lblFundBalance.Size = new System.Drawing.Size(95, 17);
            this.lblFundBalance.TabIndex = 15;
            this.lblFundBalance.Text = "FundBalance:";
            // 
            // txtFundBalance
            // 
            this.txtFundBalance.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtFundBalance.Location = new System.Drawing.Point(1011, 37);
            this.txtFundBalance.Margin = new System.Windows.Forms.Padding(2);
            this.txtFundBalance.Name = "txtFundBalance";
            this.txtFundBalance.ReadOnly = true;
            this.txtFundBalance.Size = new System.Drawing.Size(119, 23);
            this.txtFundBalance.TabIndex = 14;
            this.txtFundBalance.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblTo
            // 
            this.lblTo.AutoSize = true;
            this.lblTo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTo.ForeColor = System.Drawing.Color.White;
            this.lblTo.Location = new System.Drawing.Point(364, 38);
            this.lblTo.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblTo.Name = "lblTo";
            this.lblTo.Size = new System.Drawing.Size(29, 17);
            this.lblTo.TabIndex = 13;
            this.lblTo.Text = "To:";
            // 
            // dtpTo
            // 
            this.dtpTo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtpTo.Location = new System.Drawing.Point(395, 37);
            this.dtpTo.Margin = new System.Windows.Forms.Padding(2);
            this.dtpTo.Name = "dtpTo";
            this.dtpTo.Size = new System.Drawing.Size(270, 23);
            this.dtpTo.TabIndex = 12;
            this.dtpTo.ValueChanged += new System.EventHandler(this.dtpTo_ValueChanged);
            // 
            // lblFrom
            // 
            this.lblFrom.AutoSize = true;
            this.lblFrom.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFrom.ForeColor = System.Drawing.Color.White;
            this.lblFrom.Location = new System.Drawing.Point(57, 38);
            this.lblFrom.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblFrom.Name = "lblFrom";
            this.lblFrom.Size = new System.Drawing.Size(44, 17);
            this.lblFrom.TabIndex = 11;
            this.lblFrom.Text = "From:";
            // 
            // dtpFrom
            // 
            this.dtpFrom.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dtpFrom.Location = new System.Drawing.Point(104, 36);
            this.dtpFrom.Margin = new System.Windows.Forms.Padding(2);
            this.dtpFrom.Name = "dtpFrom";
            this.dtpFrom.Size = new System.Drawing.Size(256, 23);
            this.dtpFrom.TabIndex = 10;
            this.dtpFrom.ValueChanged += new System.EventHandler(this.dtpFrom_ValueChanged);
            // 
            // ddlFund
            // 
            this.ddlFund.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlFund.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlFund.FormattingEnabled = true;
            this.ddlFund.Location = new System.Drawing.Point(104, 6);
            this.ddlFund.Margin = new System.Windows.Forms.Padding(2);
            this.ddlFund.Name = "ddlFund";
            this.ddlFund.Size = new System.Drawing.Size(1026, 24);
            this.ddlFund.TabIndex = 1;
            this.ddlFund.SelectedIndexChanged += new System.EventHandler(this.ddlFund_SelectedIndexChanged);
            // 
            // lblFundSelector
            // 
            this.lblFundSelector.AutoEllipsis = true;
            this.lblFundSelector.AutoSize = true;
            this.lblFundSelector.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFundSelector.ForeColor = System.Drawing.Color.White;
            this.lblFundSelector.Location = new System.Drawing.Point(57, 9);
            this.lblFundSelector.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblFundSelector.Name = "lblFundSelector";
            this.lblFundSelector.Size = new System.Drawing.Size(44, 17);
            this.lblFundSelector.TabIndex = 0;
            this.lblFundSelector.Text = "Fund:";
            this.lblFundSelector.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // pnlTradeOrders
            // 
            this.pnlTradeOrders.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlTradeOrders.Controls.Add(this.lblMetrics);
            this.pnlTradeOrders.Controls.Add(this.tlpFundMetrics);
            this.pnlTradeOrders.Controls.Add(this.lblComment);
            this.pnlTradeOrders.Controls.Add(this.txtComment);
            this.pnlTradeOrders.Controls.Add(this.gridTransactions);
            this.pnlTradeOrders.Controls.Add(this.btnAdjust);
            this.pnlTradeOrders.Controls.Add(this.lblTradeOrders);
            this.pnlTradeOrders.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlTradeOrders.Location = new System.Drawing.Point(0, 67);
            this.pnlTradeOrders.Margin = new System.Windows.Forms.Padding(2);
            this.pnlTradeOrders.Name = "pnlTradeOrders";
            this.pnlTradeOrders.Size = new System.Drawing.Size(1235, 699);
            this.pnlTradeOrders.TabIndex = 2;
            // 
            // lblMetrics
            // 
            this.lblMetrics.AutoSize = true;
            this.lblMetrics.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMetrics.ForeColor = System.Drawing.Color.White;
            this.lblMetrics.Location = new System.Drawing.Point(37, 630);
            this.lblMetrics.Name = "lblMetrics";
            this.lblMetrics.Size = new System.Drawing.Size(61, 18);
            this.lblMetrics.TabIndex = 14;
            this.lblMetrics.Text = "Metrics:";
            // 
            // tlpFundMetrics
            // 
            this.tlpFundMetrics.ColumnCount = 9;
            this.tlpFundMetrics.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tlpFundMetrics.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tlpFundMetrics.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tlpFundMetrics.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tlpFundMetrics.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tlpFundMetrics.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tlpFundMetrics.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tlpFundMetrics.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tlpFundMetrics.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 11.11111F));
            this.tlpFundMetrics.Controls.Add(this.pnlWinRate, 0, 0);
            this.tlpFundMetrics.Controls.Add(this.txtWinRate, 0, 1);
            this.tlpFundMetrics.Controls.Add(this.pnlAverageProfit, 1, 0);
            this.tlpFundMetrics.Controls.Add(this.txtAverageProfit, 1, 1);
            this.tlpFundMetrics.Controls.Add(this.pnlAverageLoss, 2, 0);
            this.tlpFundMetrics.Controls.Add(this.txtLossRate, 2, 1);
            this.tlpFundMetrics.Controls.Add(this.pnlSharpeRatio, 3, 0);
            this.tlpFundMetrics.Controls.Add(this.txtAverageLoss, 3, 1);
            this.tlpFundMetrics.Controls.Add(this.pnlCommission, 8, 0);
            this.tlpFundMetrics.Controls.Add(this.txtCommission, 8, 1);
            this.tlpFundMetrics.Controls.Add(this.pnlProfitLossPercent, 7, 0);
            this.tlpFundMetrics.Controls.Add(this.txtProfitLossPercent, 7, 1);
            this.tlpFundMetrics.Controls.Add(this.pnlProfitLoss, 6, 0);
            this.tlpFundMetrics.Controls.Add(this.txtProfitLoss, 6, 1);
            this.tlpFundMetrics.Controls.Add(this.panel1, 5, 0);
            this.tlpFundMetrics.Controls.Add(this.txtSharpeRatio, 5, 1);
            this.tlpFundMetrics.Controls.Add(this.panel2, 4, 0);
            this.tlpFundMetrics.Controls.Add(this.txtWinLossRatio, 4, 1);
            this.tlpFundMetrics.Location = new System.Drawing.Point(104, 627);
            this.tlpFundMetrics.Name = "tlpFundMetrics";
            this.tlpFundMetrics.RowCount = 2;
            this.tlpFundMetrics.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpFundMetrics.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpFundMetrics.Size = new System.Drawing.Size(1026, 61);
            this.tlpFundMetrics.TabIndex = 13;
            // 
            // pnlWinRate
            // 
            this.pnlWinRate.Controls.Add(this.lblWinRate);
            this.pnlWinRate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlWinRate.Location = new System.Drawing.Point(3, 3);
            this.pnlWinRate.Name = "pnlWinRate";
            this.pnlWinRate.Size = new System.Drawing.Size(107, 24);
            this.pnlWinRate.TabIndex = 0;
            // 
            // lblWinRate
            // 
            this.lblWinRate.AutoSize = true;
            this.lblWinRate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblWinRate.ForeColor = System.Drawing.Color.White;
            this.lblWinRate.Location = new System.Drawing.Point(20, 4);
            this.lblWinRate.Name = "lblWinRate";
            this.lblWinRate.Size = new System.Drawing.Size(70, 16);
            this.lblWinRate.TabIndex = 0;
            this.lblWinRate.Text = "Win Rate";
            this.lblWinRate.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtWinRate
            // 
            this.txtWinRate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtWinRate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtWinRate.Location = new System.Drawing.Point(3, 33);
            this.txtWinRate.Name = "txtWinRate";
            this.txtWinRate.Size = new System.Drawing.Size(107, 22);
            this.txtWinRate.TabIndex = 1;
            this.txtWinRate.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // pnlAverageProfit
            // 
            this.pnlAverageProfit.Controls.Add(this.lblAverageProfit);
            this.pnlAverageProfit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlAverageProfit.Location = new System.Drawing.Point(116, 3);
            this.pnlAverageProfit.Name = "pnlAverageProfit";
            this.pnlAverageProfit.Size = new System.Drawing.Size(107, 24);
            this.pnlAverageProfit.TabIndex = 2;
            // 
            // lblAverageProfit
            // 
            this.lblAverageProfit.AutoSize = true;
            this.lblAverageProfit.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblAverageProfit.ForeColor = System.Drawing.Color.White;
            this.lblAverageProfit.Location = new System.Drawing.Point(16, 4);
            this.lblAverageProfit.Name = "lblAverageProfit";
            this.lblAverageProfit.Size = new System.Drawing.Size(74, 16);
            this.lblAverageProfit.TabIndex = 0;
            this.lblAverageProfit.Text = "Avg Profit";
            this.lblAverageProfit.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtAverageProfit
            // 
            this.txtAverageProfit.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtAverageProfit.Location = new System.Drawing.Point(116, 33);
            this.txtAverageProfit.Name = "txtAverageProfit";
            this.txtAverageProfit.Size = new System.Drawing.Size(107, 22);
            this.txtAverageProfit.TabIndex = 3;
            this.txtAverageProfit.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // pnlAverageLoss
            // 
            this.pnlAverageLoss.Controls.Add(this.lblLossRate);
            this.pnlAverageLoss.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlAverageLoss.Location = new System.Drawing.Point(229, 3);
            this.pnlAverageLoss.Name = "pnlAverageLoss";
            this.pnlAverageLoss.Size = new System.Drawing.Size(107, 24);
            this.pnlAverageLoss.TabIndex = 4;
            // 
            // lblLossRate
            // 
            this.lblLossRate.AutoSize = true;
            this.lblLossRate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLossRate.ForeColor = System.Drawing.Color.White;
            this.lblLossRate.Location = new System.Drawing.Point(17, 4);
            this.lblLossRate.Name = "lblLossRate";
            this.lblLossRate.Size = new System.Drawing.Size(77, 16);
            this.lblLossRate.TabIndex = 0;
            this.lblLossRate.Text = "Loss Rate";
            this.lblLossRate.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtLossRate
            // 
            this.txtLossRate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLossRate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtLossRate.Location = new System.Drawing.Point(229, 33);
            this.txtLossRate.Name = "txtLossRate";
            this.txtLossRate.Size = new System.Drawing.Size(107, 22);
            this.txtLossRate.TabIndex = 5;
            this.txtLossRate.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtAverageLoss
            // 
            this.txtAverageLoss.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtAverageLoss.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtAverageLoss.Location = new System.Drawing.Point(342, 33);
            this.txtAverageLoss.Name = "txtAverageLoss";
            this.txtAverageLoss.Size = new System.Drawing.Size(107, 22);
            this.txtAverageLoss.TabIndex = 7;
            this.txtAverageLoss.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // pnlProfitLoss
            // 
            this.pnlProfitLoss.Controls.Add(this.lblProfitLoss);
            this.pnlProfitLoss.Location = new System.Drawing.Point(681, 3);
            this.pnlProfitLoss.Name = "pnlProfitLoss";
            this.pnlProfitLoss.Size = new System.Drawing.Size(107, 24);
            this.pnlProfitLoss.TabIndex = 8;
            // 
            // lblProfitLoss
            // 
            this.lblProfitLoss.AutoSize = true;
            this.lblProfitLoss.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblProfitLoss.ForeColor = System.Drawing.Color.White;
            this.lblProfitLoss.Location = new System.Drawing.Point(28, 4);
            this.lblProfitLoss.Name = "lblProfitLoss";
            this.lblProfitLoss.Size = new System.Drawing.Size(51, 16);
            this.lblProfitLoss.TabIndex = 0;
            this.lblProfitLoss.Text = "Pnl ($)";
            this.lblProfitLoss.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pnlProfitLossPercent
            // 
            this.pnlProfitLossPercent.Controls.Add(this.lblProfitLossPercent);
            this.pnlProfitLossPercent.Location = new System.Drawing.Point(794, 3);
            this.pnlProfitLossPercent.Name = "pnlProfitLossPercent";
            this.pnlProfitLossPercent.Size = new System.Drawing.Size(107, 24);
            this.pnlProfitLossPercent.TabIndex = 9;
            // 
            // lblProfitLossPercent
            // 
            this.lblProfitLossPercent.AutoSize = true;
            this.lblProfitLossPercent.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblProfitLossPercent.ForeColor = System.Drawing.Color.White;
            this.lblProfitLossPercent.Location = new System.Drawing.Point(25, 4);
            this.lblProfitLossPercent.Name = "lblProfitLossPercent";
            this.lblProfitLossPercent.Size = new System.Drawing.Size(56, 16);
            this.lblProfitLossPercent.TabIndex = 0;
            this.lblProfitLossPercent.Text = "Pnl (%)";
            this.lblProfitLossPercent.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtProfitLoss
            // 
            this.txtProfitLoss.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtProfitLoss.Location = new System.Drawing.Point(681, 33);
            this.txtProfitLoss.Name = "txtProfitLoss";
            this.txtProfitLoss.Size = new System.Drawing.Size(107, 22);
            this.txtProfitLoss.TabIndex = 11;
            this.txtProfitLoss.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtProfitLossPercent
            // 
            this.txtProfitLossPercent.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtProfitLossPercent.Location = new System.Drawing.Point(794, 33);
            this.txtProfitLossPercent.Name = "txtProfitLossPercent";
            this.txtProfitLossPercent.Size = new System.Drawing.Size(107, 22);
            this.txtProfitLossPercent.TabIndex = 12;
            this.txtProfitLossPercent.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtCommission
            // 
            this.txtCommission.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtCommission.Location = new System.Drawing.Point(907, 33);
            this.txtCommission.Name = "txtCommission";
            this.txtCommission.Size = new System.Drawing.Size(116, 22);
            this.txtCommission.TabIndex = 13;
            this.txtCommission.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblComment
            // 
            this.lblComment.AutoSize = true;
            this.lblComment.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblComment.ForeColor = System.Drawing.Color.White;
            this.lblComment.Location = new System.Drawing.Point(29, 548);
            this.lblComment.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblComment.Name = "lblComment";
            this.lblComment.Size = new System.Drawing.Size(71, 17);
            this.lblComment.TabIndex = 12;
            this.lblComment.Text = "Comment:";
            // 
            // txtComment
            // 
            this.txtComment.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtComment.Location = new System.Drawing.Point(104, 548);
            this.txtComment.Margin = new System.Windows.Forms.Padding(2);
            this.txtComment.Multiline = true;
            this.txtComment.Name = "txtComment";
            this.txtComment.ReadOnly = true;
            this.txtComment.Size = new System.Drawing.Size(1026, 70);
            this.txtComment.TabIndex = 11;
            // 
            // gridTransactions
            // 
            this.gridTransactions.AllowUserToDeleteRows = false;
            this.gridTransactions.AutoGenerateColumns = false;
            this.gridTransactions.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridTransactions.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridTransactions.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridTransactions.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.transactionIdDataGridViewTextBoxColumn,
            this.transactionDateDataGridViewTextBoxColumn,
            this.transactionTypeDataGridViewTextBoxColumn,
            this.orderIdDataGridViewTextBoxColumn,
            this.tradeIdDataGridViewTextBoxColumn,
            this.tradeTypeDataGridViewTextBoxColumn,
            this.valueDateDataGridViewTextBoxColumn,
            this.tradeStatusDataGridViewTextBoxColumn,
            this.amountDataGridViewTextBoxColumn,
            this.balanceDataGridViewTextBoxColumn});
            this.gridTransactions.DataSource = this.fundTransactionBindingSource;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridTransactions.DefaultCellStyle = dataGridViewCellStyle4;
            this.gridTransactions.Location = new System.Drawing.Point(104, 4);
            this.gridTransactions.Margin = new System.Windows.Forms.Padding(2);
            this.gridTransactions.MultiSelect = false;
            this.gridTransactions.Name = "gridTransactions";
            this.gridTransactions.ReadOnly = true;
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridTransactions.RowHeadersDefaultCellStyle = dataGridViewCellStyle5;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gridTransactions.RowsDefaultCellStyle = dataGridViewCellStyle6;
            this.gridTransactions.RowTemplate.Height = 24;
            this.gridTransactions.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridTransactions.Size = new System.Drawing.Size(1026, 540);
            this.gridTransactions.TabIndex = 10;
            this.gridTransactions.SelectionChanged += new System.EventHandler(this.gridTransactions_SelectionChanged);
            // 
            // transactionIdDataGridViewTextBoxColumn
            // 
            this.transactionIdDataGridViewTextBoxColumn.DataPropertyName = "TransactionId";
            this.transactionIdDataGridViewTextBoxColumn.HeaderText = "ID";
            this.transactionIdDataGridViewTextBoxColumn.Name = "transactionIdDataGridViewTextBoxColumn";
            this.transactionIdDataGridViewTextBoxColumn.ReadOnly = true;
            this.transactionIdDataGridViewTextBoxColumn.Width = 70;
            // 
            // transactionDateDataGridViewTextBoxColumn
            // 
            this.transactionDateDataGridViewTextBoxColumn.DataPropertyName = "TransactionDate";
            this.transactionDateDataGridViewTextBoxColumn.HeaderText = "Date";
            this.transactionDateDataGridViewTextBoxColumn.Name = "transactionDateDataGridViewTextBoxColumn";
            this.transactionDateDataGridViewTextBoxColumn.ReadOnly = true;
            this.transactionDateDataGridViewTextBoxColumn.Width = 140;
            // 
            // transactionTypeDataGridViewTextBoxColumn
            // 
            this.transactionTypeDataGridViewTextBoxColumn.DataPropertyName = "TransactionType";
            this.transactionTypeDataGridViewTextBoxColumn.HeaderText = "Type";
            this.transactionTypeDataGridViewTextBoxColumn.Name = "transactionTypeDataGridViewTextBoxColumn";
            this.transactionTypeDataGridViewTextBoxColumn.ReadOnly = true;
            this.transactionTypeDataGridViewTextBoxColumn.Width = 140;
            // 
            // orderIdDataGridViewTextBoxColumn
            // 
            this.orderIdDataGridViewTextBoxColumn.DataPropertyName = "OrderId";
            this.orderIdDataGridViewTextBoxColumn.HeaderText = "OrderId";
            this.orderIdDataGridViewTextBoxColumn.Name = "orderIdDataGridViewTextBoxColumn";
            this.orderIdDataGridViewTextBoxColumn.ReadOnly = true;
            this.orderIdDataGridViewTextBoxColumn.Width = 70;
            // 
            // tradeIdDataGridViewTextBoxColumn
            // 
            this.tradeIdDataGridViewTextBoxColumn.DataPropertyName = "TradeId";
            this.tradeIdDataGridViewTextBoxColumn.HeaderText = "TradeId";
            this.tradeIdDataGridViewTextBoxColumn.Name = "tradeIdDataGridViewTextBoxColumn";
            this.tradeIdDataGridViewTextBoxColumn.ReadOnly = true;
            this.tradeIdDataGridViewTextBoxColumn.Width = 70;
            // 
            // tradeTypeDataGridViewTextBoxColumn
            // 
            this.tradeTypeDataGridViewTextBoxColumn.DataPropertyName = "TradeType";
            this.tradeTypeDataGridViewTextBoxColumn.HeaderText = "TradeType";
            this.tradeTypeDataGridViewTextBoxColumn.Name = "tradeTypeDataGridViewTextBoxColumn";
            this.tradeTypeDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // valueDateDataGridViewTextBoxColumn
            // 
            this.valueDateDataGridViewTextBoxColumn.DataPropertyName = "ValueDate";
            this.valueDateDataGridViewTextBoxColumn.HeaderText = "ValueDate";
            this.valueDateDataGridViewTextBoxColumn.Name = "valueDateDataGridViewTextBoxColumn";
            this.valueDateDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // tradeStatusDataGridViewTextBoxColumn
            // 
            this.tradeStatusDataGridViewTextBoxColumn.DataPropertyName = "TradeStatus";
            this.tradeStatusDataGridViewTextBoxColumn.HeaderText = "TradeStatus";
            this.tradeStatusDataGridViewTextBoxColumn.Name = "tradeStatusDataGridViewTextBoxColumn";
            this.tradeStatusDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // amountDataGridViewTextBoxColumn
            // 
            this.amountDataGridViewTextBoxColumn.DataPropertyName = "Amount";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle2.Format = "C2";
            dataGridViewCellStyle2.NullValue = null;
            this.amountDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle2;
            this.amountDataGridViewTextBoxColumn.HeaderText = "Amount";
            this.amountDataGridViewTextBoxColumn.Name = "amountDataGridViewTextBoxColumn";
            this.amountDataGridViewTextBoxColumn.ReadOnly = true;
            this.amountDataGridViewTextBoxColumn.Width = 95;
            // 
            // balanceDataGridViewTextBoxColumn
            // 
            this.balanceDataGridViewTextBoxColumn.DataPropertyName = "Balance";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle3.Format = "C2";
            dataGridViewCellStyle3.NullValue = null;
            this.balanceDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle3;
            this.balanceDataGridViewTextBoxColumn.HeaderText = "Balance";
            this.balanceDataGridViewTextBoxColumn.Name = "balanceDataGridViewTextBoxColumn";
            this.balanceDataGridViewTextBoxColumn.ReadOnly = true;
            this.balanceDataGridViewTextBoxColumn.Width = 95;
            // 
            // fundTransactionBindingSource
            // 
            this.fundTransactionBindingSource.DataSource = typeof(TomasAI.IFM.Shared.Fund.ViewModels.FundTransactionReadModel);
            // 
            // btnAdjust
            // 
            this.btnAdjust.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAdjust.Location = new System.Drawing.Point(1141, 4);
            this.btnAdjust.Margin = new System.Windows.Forms.Padding(2);
            this.btnAdjust.Name = "btnAdjust";
            this.btnAdjust.Size = new System.Drawing.Size(83, 28);
            this.btnAdjust.TabIndex = 4;
            this.btnAdjust.Text = "Adjust...";
            this.btnAdjust.UseVisualStyleBackColor = true;
            this.btnAdjust.Click += new System.EventHandler(this.btnAdjust_Click);
            // 
            // lblTradeOrders
            // 
            this.lblTradeOrders.AutoSize = true;
            this.lblTradeOrders.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTradeOrders.ForeColor = System.Drawing.Color.White;
            this.lblTradeOrders.Location = new System.Drawing.Point(10, 4);
            this.lblTradeOrders.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblTradeOrders.Name = "lblTradeOrders";
            this.lblTradeOrders.Size = new System.Drawing.Size(94, 17);
            this.lblTradeOrders.TabIndex = 1;
            this.lblTradeOrders.Text = "Transactions:";
            this.lblTradeOrders.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lblCommission
            // 
            this.lblCommission.AutoSize = true;
            this.lblCommission.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCommission.ForeColor = System.Drawing.Color.White;
            this.lblCommission.Location = new System.Drawing.Point(13, 4);
            this.lblCommission.Name = "lblCommission";
            this.lblCommission.Size = new System.Drawing.Size(91, 16);
            this.lblCommission.TabIndex = 0;
            this.lblCommission.Text = "Commission";
            this.lblCommission.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pnlCommission
            // 
            this.pnlCommission.Controls.Add(this.lblCommission);
            this.pnlCommission.Location = new System.Drawing.Point(907, 3);
            this.pnlCommission.Name = "pnlCommission";
            this.pnlCommission.Size = new System.Drawing.Size(107, 24);
            this.pnlCommission.TabIndex = 10;
            // 
            // lblAverageLoss
            // 
            this.lblAverageLoss.AutoSize = true;
            this.lblAverageLoss.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblAverageLoss.ForeColor = System.Drawing.Color.White;
            this.lblAverageLoss.Location = new System.Drawing.Point(18, 4);
            this.lblAverageLoss.Name = "lblAverageLoss";
            this.lblAverageLoss.Size = new System.Drawing.Size(71, 16);
            this.lblAverageLoss.TabIndex = 0;
            this.lblAverageLoss.Text = "Avg Loss";
            this.lblAverageLoss.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pnlSharpeRatio
            // 
            this.pnlSharpeRatio.Controls.Add(this.lblAverageLoss);
            this.pnlSharpeRatio.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSharpeRatio.Location = new System.Drawing.Point(342, 3);
            this.pnlSharpeRatio.Name = "pnlSharpeRatio";
            this.pnlSharpeRatio.Size = new System.Drawing.Size(107, 24);
            this.pnlSharpeRatio.TabIndex = 6;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.lblSharpeRatio);
            this.panel1.Location = new System.Drawing.Point(568, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(107, 24);
            this.panel1.TabIndex = 14;
            // 
            // txtSharpeRatio
            // 
            this.txtSharpeRatio.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtSharpeRatio.Location = new System.Drawing.Point(568, 33);
            this.txtSharpeRatio.Name = "txtSharpeRatio";
            this.txtSharpeRatio.Size = new System.Drawing.Size(107, 22);
            this.txtSharpeRatio.TabIndex = 15;
            this.txtSharpeRatio.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblSharpeRatio
            // 
            this.lblSharpeRatio.AutoSize = true;
            this.lblSharpeRatio.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSharpeRatio.ForeColor = System.Drawing.Color.White;
            this.lblSharpeRatio.Location = new System.Drawing.Point(5, 4);
            this.lblSharpeRatio.Name = "lblSharpeRatio";
            this.lblSharpeRatio.Size = new System.Drawing.Size(98, 16);
            this.lblSharpeRatio.TabIndex = 0;
            this.lblSharpeRatio.Text = "Sharpe Ratio";
            this.lblSharpeRatio.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.lblWinLossRatio);
            this.panel2.Location = new System.Drawing.Point(455, 3);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(107, 24);
            this.panel2.TabIndex = 16;
            // 
            // lblWinLossRatio
            // 
            this.lblWinLossRatio.AutoSize = true;
            this.lblWinLossRatio.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblWinLossRatio.ForeColor = System.Drawing.Color.White;
            this.lblWinLossRatio.Location = new System.Drawing.Point(18, 4);
            this.lblWinLossRatio.Name = "lblWinLossRatio";
            this.lblWinLossRatio.Size = new System.Drawing.Size(75, 16);
            this.lblWinLossRatio.TabIndex = 0;
            this.lblWinLossRatio.Text = "W/L Ratio";
            this.lblWinLossRatio.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtWinLossRatio
            // 
            this.txtWinLossRatio.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtWinLossRatio.Location = new System.Drawing.Point(455, 33);
            this.txtWinLossRatio.Name = "txtWinLossRatio";
            this.txtWinLossRatio.Size = new System.Drawing.Size(106, 22);
            this.txtWinLossRatio.TabIndex = 17;
            this.txtWinLossRatio.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // FundTransactionEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1235, 766);
            this.Controls.Add(this.pnlTradeOrders);
            this.Controls.Add(this.pnlFundSelector);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FundTransactionEditor";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Fund Transactions Editor";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FundTransactionEditor_FormClosed);
            this.Load += new System.EventHandler(this.FundTransactionEditor_Load);
            this.pnlFundSelector.ResumeLayout(false);
            this.pnlFundSelector.PerformLayout();
            this.pnlTradeOrders.ResumeLayout(false);
            this.pnlTradeOrders.PerformLayout();
            this.tlpFundMetrics.ResumeLayout(false);
            this.tlpFundMetrics.PerformLayout();
            this.pnlWinRate.ResumeLayout(false);
            this.pnlWinRate.PerformLayout();
            this.pnlAverageProfit.ResumeLayout(false);
            this.pnlAverageProfit.PerformLayout();
            this.pnlAverageLoss.ResumeLayout(false);
            this.pnlAverageLoss.PerformLayout();
            this.pnlProfitLoss.ResumeLayout(false);
            this.pnlProfitLoss.PerformLayout();
            this.pnlProfitLossPercent.ResumeLayout(false);
            this.pnlProfitLossPercent.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridTransactions)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.fundTransactionBindingSource)).EndInit();
            this.pnlCommission.ResumeLayout(false);
            this.pnlCommission.PerformLayout();
            this.pnlSharpeRatio.ResumeLayout(false);
            this.pnlSharpeRatio.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlFundSelector;
        private System.Windows.Forms.ComboBox ddlFund;
        private System.Windows.Forms.Label lblFundSelector;
        private System.Windows.Forms.Panel pnlTradeOrders;
        private System.Windows.Forms.DataGridView gridTransactions;
        private System.Windows.Forms.Button btnAdjust;
        private System.Windows.Forms.Label lblTradeOrders;
        private System.Windows.Forms.Label lblTo;
        private System.Windows.Forms.DateTimePicker dtpTo;
        private System.Windows.Forms.Label lblFrom;
        private System.Windows.Forms.DateTimePicker dtpFrom;
        private System.Windows.Forms.Label lblComment;
        private System.Windows.Forms.TextBox txtComment;
        private System.Windows.Forms.BindingSource fundTransactionBindingSource;
        private System.Windows.Forms.Label lblFundBalance;
        private System.Windows.Forms.TextBox txtFundBalance;
        private System.Windows.Forms.DataGridViewTextBoxColumn transactionIdDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn transactionDateDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn transactionTypeDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn orderIdDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn tradeIdDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn tradeTypeDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn valueDateDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn tradeStatusDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn amountDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn balanceDataGridViewTextBoxColumn;
        private System.Windows.Forms.TableLayoutPanel tlpFundMetrics;
        private System.Windows.Forms.Label lblMetrics;
        private System.Windows.Forms.Panel pnlWinRate;
        private System.Windows.Forms.Label lblWinRate;
        private System.Windows.Forms.TextBox txtWinRate;
        private System.Windows.Forms.Panel pnlAverageProfit;
        private System.Windows.Forms.Label lblAverageProfit;
        private System.Windows.Forms.TextBox txtAverageProfit;
        private System.Windows.Forms.Panel pnlAverageLoss;
        private System.Windows.Forms.Label lblLossRate;
        private System.Windows.Forms.TextBox txtLossRate;
        private System.Windows.Forms.TextBox txtAverageLoss;
        private System.Windows.Forms.Panel pnlProfitLoss;
        private System.Windows.Forms.Label lblProfitLoss;
        private System.Windows.Forms.Panel pnlProfitLossPercent;
        private System.Windows.Forms.Label lblProfitLossPercent;
        private System.Windows.Forms.TextBox txtProfitLoss;
        private System.Windows.Forms.TextBox txtProfitLossPercent;
        private System.Windows.Forms.TextBox txtCommission;
        private System.Windows.Forms.Panel pnlSharpeRatio;
        private System.Windows.Forms.Label lblAverageLoss;
        private System.Windows.Forms.Panel pnlCommission;
        private System.Windows.Forms.Label lblCommission;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lblSharpeRatio;
        private System.Windows.Forms.TextBox txtSharpeRatio;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label lblWinLossRatio;
        private System.Windows.Forms.TextBox txtWinLossRatio;
    }
}