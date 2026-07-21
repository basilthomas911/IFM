namespace TomasAI.IFM.UI.Net.Views.Fund
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
            components = new System.ComponentModel.Container();
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FundTransactionEditor));
            pnlFundSelector = new Panel();
            btnAdjust = new Button();
            lblFundBalance = new Label();
            txtFundBalance = new TextBox();
            lblTo = new Label();
            dtpTo = new DateTimePicker();
            lblFrom = new Label();
            dtpFrom = new DateTimePicker();
            ddlFund = new ComboBox();
            lblFundSelector = new Label();
            pnlTradeOrders = new Panel();
            lblMetrics = new Label();
            tlpFundMetrics = new TableLayoutPanel();
            pnlWinRate = new Panel();
            lblWinRate = new Label();
            txtWinRate = new TextBox();
            pnlAverageProfit = new Panel();
            lblAverageProfit = new Label();
            txtAverageProfit = new TextBox();
            pnlAverageLoss = new Panel();
            lblLossRate = new Label();
            txtLossRate = new TextBox();
            pnlSharpeRatio = new Panel();
            lblAverageLoss = new Label();
            txtAverageLoss = new TextBox();
            pnlCommission = new Panel();
            lblCommission = new Label();
            txtCommission = new TextBox();
            pnlProfitLossPercent = new Panel();
            lblProfitLossPercent = new Label();
            txtProfitLossPercent = new TextBox();
            pnlProfitLoss = new Panel();
            lblProfitLoss = new Label();
            txtProfitLoss = new TextBox();
            panel1 = new Panel();
            lblSharpeRatio = new Label();
            txtSharpeRatio = new TextBox();
            panel2 = new Panel();
            lblWinLossRatio = new Label();
            txtWinLossRatio = new TextBox();
            lblComment = new Label();
            txtComment = new TextBox();
            gridTransactions = new DataGridView();
            lblTradeOrders = new Label();
            fundTransactionsBindingSource = new BindingSource(components);
            pnlFundSelector.SuspendLayout();
            pnlTradeOrders.SuspendLayout();
            tlpFundMetrics.SuspendLayout();
            pnlWinRate.SuspendLayout();
            pnlAverageProfit.SuspendLayout();
            pnlAverageLoss.SuspendLayout();
            pnlSharpeRatio.SuspendLayout();
            pnlCommission.SuspendLayout();
            pnlProfitLossPercent.SuspendLayout();
            pnlProfitLoss.SuspendLayout();
            panel1.SuspendLayout();
            panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridTransactions).BeginInit();
            ((System.ComponentModel.ISupportInitialize)fundTransactionsBindingSource).BeginInit();
            SuspendLayout();
            // 
            // pnlFundSelector
            // 
            pnlFundSelector.BackColor = Color.FromArgb(64, 64, 64);
            pnlFundSelector.Controls.Add(btnAdjust);
            pnlFundSelector.Controls.Add(lblFundBalance);
            pnlFundSelector.Controls.Add(txtFundBalance);
            pnlFundSelector.Controls.Add(lblTo);
            pnlFundSelector.Controls.Add(dtpTo);
            pnlFundSelector.Controls.Add(lblFrom);
            pnlFundSelector.Controls.Add(dtpFrom);
            pnlFundSelector.Controls.Add(ddlFund);
            pnlFundSelector.Controls.Add(lblFundSelector);
            pnlFundSelector.Dock = DockStyle.Top;
            pnlFundSelector.Location = new Point(0, 0);
            pnlFundSelector.Margin = new Padding(2);
            pnlFundSelector.Name = "pnlFundSelector";
            pnlFundSelector.Size = new Size(2176, 37);
            pnlFundSelector.TabIndex = 1;
            // 
            // btnAdjust
            // 
            btnAdjust.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnAdjust.ForeColor = Color.Black;
            btnAdjust.Location = new Point(2058, 2);
            btnAdjust.Margin = new Padding(2);
            btnAdjust.Name = "btnAdjust";
            btnAdjust.Size = new Size(113, 28);
            btnAdjust.TabIndex = 16;
            btnAdjust.Text = "Adjust...";
            btnAdjust.UseVisualStyleBackColor = true;
            btnAdjust.Click += btnAdjust_Click_1;
            // 
            // lblFundBalance
            // 
            lblFundBalance.AutoSize = true;
            lblFundBalance.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblFundBalance.ForeColor = Color.White;
            lblFundBalance.Location = new Point(1747, 6);
            lblFundBalance.Margin = new Padding(2, 0, 2, 0);
            lblFundBalance.Name = "lblFundBalance";
            lblFundBalance.Size = new Size(63, 17);
            lblFundBalance.TabIndex = 15;
            lblFundBalance.Text = "Balance:";
            // 
            // txtFundBalance
            // 
            txtFundBalance.BackColor = Color.Black;
            txtFundBalance.BorderStyle = BorderStyle.FixedSingle;
            txtFundBalance.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtFundBalance.ForeColor = Color.White;
            txtFundBalance.Location = new Point(1814, 3);
            txtFundBalance.Margin = new Padding(2);
            txtFundBalance.Name = "txtFundBalance";
            txtFundBalance.ReadOnly = true;
            txtFundBalance.Size = new Size(113, 23);
            txtFundBalance.TabIndex = 14;
            txtFundBalance.TextAlign = HorizontalAlignment.Right;
            // 
            // lblTo
            // 
            lblTo.AutoSize = true;
            lblTo.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTo.ForeColor = Color.White;
            lblTo.Location = new Point(1442, 6);
            lblTo.Margin = new Padding(2, 0, 2, 0);
            lblTo.Name = "lblTo";
            lblTo.Size = new Size(29, 17);
            lblTo.TabIndex = 13;
            lblTo.Text = "To:";
            // 
            // dtpTo
            // 
            dtpTo.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dtpTo.Location = new Point(1473, 4);
            dtpTo.Margin = new Padding(2);
            dtpTo.Name = "dtpTo";
            dtpTo.Size = new Size(270, 23);
            dtpTo.TabIndex = 12;
            dtpTo.ValueChanged += dtpTo_ValueChanged;
            // 
            // lblFrom
            // 
            lblFrom.AutoSize = true;
            lblFrom.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblFrom.ForeColor = Color.White;
            lblFrom.Location = new Point(1135, 7);
            lblFrom.Margin = new Padding(2, 0, 2, 0);
            lblFrom.Name = "lblFrom";
            lblFrom.Size = new Size(44, 17);
            lblFrom.TabIndex = 11;
            lblFrom.Text = "From:";
            // 
            // dtpFrom
            // 
            dtpFrom.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dtpFrom.Location = new Point(1182, 3);
            dtpFrom.Margin = new Padding(2);
            dtpFrom.Name = "dtpFrom";
            dtpFrom.Size = new Size(256, 23);
            dtpFrom.TabIndex = 10;
            dtpFrom.ValueChanged += dtpFrom_ValueChanged;
            // 
            // ddlFund
            // 
            ddlFund.BackColor = SystemColors.WindowText;
            ddlFund.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlFund.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlFund.ForeColor = SystemColors.Window;
            ddlFund.FormattingEnabled = true;
            ddlFund.Location = new Point(104, 6);
            ddlFund.Margin = new Padding(2);
            ddlFund.Name = "ddlFund";
            ddlFund.Size = new Size(1026, 24);
            ddlFund.TabIndex = 1;
            ddlFund.SelectedIndexChanged += ddlFund_SelectedIndexChanged;
            // 
            // lblFundSelector
            // 
            lblFundSelector.AutoEllipsis = true;
            lblFundSelector.AutoSize = true;
            lblFundSelector.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblFundSelector.ForeColor = Color.White;
            lblFundSelector.Location = new Point(56, 9);
            lblFundSelector.Margin = new Padding(2, 0, 2, 0);
            lblFundSelector.Name = "lblFundSelector";
            lblFundSelector.Size = new Size(44, 17);
            lblFundSelector.TabIndex = 0;
            lblFundSelector.Text = "Fund:";
            lblFundSelector.TextAlign = ContentAlignment.MiddleRight;
            // 
            // pnlTradeOrders
            // 
            pnlTradeOrders.BackColor = Color.FromArgb(64, 64, 64);
            pnlTradeOrders.Controls.Add(lblMetrics);
            pnlTradeOrders.Controls.Add(tlpFundMetrics);
            pnlTradeOrders.Controls.Add(lblComment);
            pnlTradeOrders.Controls.Add(txtComment);
            pnlTradeOrders.Controls.Add(gridTransactions);
            pnlTradeOrders.Controls.Add(lblTradeOrders);
            pnlTradeOrders.Dock = DockStyle.Fill;
            pnlTradeOrders.Location = new Point(0, 37);
            pnlTradeOrders.Margin = new Padding(2);
            pnlTradeOrders.Name = "pnlTradeOrders";
            pnlTradeOrders.Size = new Size(2176, 729);
            pnlTradeOrders.TabIndex = 2;
            // 
            // lblMetrics
            // 
            lblMetrics.AutoSize = true;
            lblMetrics.Font = new Font("Microsoft Sans Serif", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblMetrics.ForeColor = Color.White;
            lblMetrics.Location = new Point(37, 666);
            lblMetrics.Name = "lblMetrics";
            lblMetrics.Size = new Size(61, 18);
            lblMetrics.TabIndex = 14;
            lblMetrics.Text = "Metrics:";
            // 
            // tlpFundMetrics
            // 
            tlpFundMetrics.ColumnCount = 9;
            tlpFundMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11111F));
            tlpFundMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11111F));
            tlpFundMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11111F));
            tlpFundMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11111F));
            tlpFundMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11111F));
            tlpFundMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11111F));
            tlpFundMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11111F));
            tlpFundMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11111F));
            tlpFundMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11111F));
            tlpFundMetrics.Controls.Add(pnlWinRate, 0, 0);
            tlpFundMetrics.Controls.Add(txtWinRate, 0, 1);
            tlpFundMetrics.Controls.Add(pnlAverageProfit, 1, 0);
            tlpFundMetrics.Controls.Add(txtAverageProfit, 1, 1);
            tlpFundMetrics.Controls.Add(pnlAverageLoss, 2, 0);
            tlpFundMetrics.Controls.Add(txtLossRate, 2, 1);
            tlpFundMetrics.Controls.Add(pnlSharpeRatio, 3, 0);
            tlpFundMetrics.Controls.Add(txtAverageLoss, 3, 1);
            tlpFundMetrics.Controls.Add(pnlCommission, 8, 0);
            tlpFundMetrics.Controls.Add(txtCommission, 8, 1);
            tlpFundMetrics.Controls.Add(pnlProfitLossPercent, 7, 0);
            tlpFundMetrics.Controls.Add(txtProfitLossPercent, 7, 1);
            tlpFundMetrics.Controls.Add(pnlProfitLoss, 6, 0);
            tlpFundMetrics.Controls.Add(txtProfitLoss, 6, 1);
            tlpFundMetrics.Controls.Add(panel1, 5, 0);
            tlpFundMetrics.Controls.Add(txtSharpeRatio, 5, 1);
            tlpFundMetrics.Controls.Add(panel2, 4, 0);
            tlpFundMetrics.Controls.Add(txtWinLossRatio, 4, 1);
            tlpFundMetrics.Location = new Point(104, 666);
            tlpFundMetrics.Name = "tlpFundMetrics";
            tlpFundMetrics.RowCount = 2;
            tlpFundMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlpFundMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tlpFundMetrics.Size = new Size(1026, 60);
            tlpFundMetrics.TabIndex = 13;
            // 
            // pnlWinRate
            // 
            pnlWinRate.Controls.Add(lblWinRate);
            pnlWinRate.Dock = DockStyle.Fill;
            pnlWinRate.Location = new Point(3, 3);
            pnlWinRate.Name = "pnlWinRate";
            pnlWinRate.Size = new Size(108, 24);
            pnlWinRate.TabIndex = 0;
            // 
            // lblWinRate
            // 
            lblWinRate.AutoSize = true;
            lblWinRate.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblWinRate.ForeColor = Color.White;
            lblWinRate.Location = new Point(20, 4);
            lblWinRate.Name = "lblWinRate";
            lblWinRate.Size = new Size(70, 16);
            lblWinRate.TabIndex = 0;
            lblWinRate.Text = "Win Rate";
            lblWinRate.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtWinRate
            // 
            txtWinRate.BackColor = Color.Black;
            txtWinRate.BorderStyle = BorderStyle.FixedSingle;
            txtWinRate.Dock = DockStyle.Fill;
            txtWinRate.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtWinRate.ForeColor = Color.White;
            txtWinRate.Location = new Point(3, 33);
            txtWinRate.Name = "txtWinRate";
            txtWinRate.Size = new Size(108, 22);
            txtWinRate.TabIndex = 1;
            txtWinRate.TextAlign = HorizontalAlignment.Center;
            // 
            // pnlAverageProfit
            // 
            pnlAverageProfit.Controls.Add(lblAverageProfit);
            pnlAverageProfit.Dock = DockStyle.Fill;
            pnlAverageProfit.Location = new Point(117, 3);
            pnlAverageProfit.Name = "pnlAverageProfit";
            pnlAverageProfit.Size = new Size(108, 24);
            pnlAverageProfit.TabIndex = 2;
            // 
            // lblAverageProfit
            // 
            lblAverageProfit.AutoSize = true;
            lblAverageProfit.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblAverageProfit.ForeColor = Color.White;
            lblAverageProfit.Location = new Point(16, 4);
            lblAverageProfit.Name = "lblAverageProfit";
            lblAverageProfit.Size = new Size(74, 16);
            lblAverageProfit.TabIndex = 0;
            lblAverageProfit.Text = "Avg Profit";
            lblAverageProfit.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtAverageProfit
            // 
            txtAverageProfit.BackColor = Color.Black;
            txtAverageProfit.BorderStyle = BorderStyle.FixedSingle;
            txtAverageProfit.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtAverageProfit.ForeColor = Color.White;
            txtAverageProfit.Location = new Point(117, 33);
            txtAverageProfit.Name = "txtAverageProfit";
            txtAverageProfit.Size = new Size(107, 22);
            txtAverageProfit.TabIndex = 3;
            txtAverageProfit.TextAlign = HorizontalAlignment.Center;
            // 
            // pnlAverageLoss
            // 
            pnlAverageLoss.Controls.Add(lblLossRate);
            pnlAverageLoss.Dock = DockStyle.Fill;
            pnlAverageLoss.Location = new Point(231, 3);
            pnlAverageLoss.Name = "pnlAverageLoss";
            pnlAverageLoss.Size = new Size(108, 24);
            pnlAverageLoss.TabIndex = 4;
            // 
            // lblLossRate
            // 
            lblLossRate.AutoSize = true;
            lblLossRate.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblLossRate.ForeColor = Color.White;
            lblLossRate.Location = new Point(17, 4);
            lblLossRate.Name = "lblLossRate";
            lblLossRate.Size = new Size(77, 16);
            lblLossRate.TabIndex = 0;
            lblLossRate.Text = "Loss Rate";
            lblLossRate.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtLossRate
            // 
            txtLossRate.BackColor = Color.Black;
            txtLossRate.BorderStyle = BorderStyle.FixedSingle;
            txtLossRate.Dock = DockStyle.Fill;
            txtLossRate.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtLossRate.ForeColor = Color.White;
            txtLossRate.Location = new Point(231, 33);
            txtLossRate.Name = "txtLossRate";
            txtLossRate.Size = new Size(108, 22);
            txtLossRate.TabIndex = 5;
            txtLossRate.TextAlign = HorizontalAlignment.Center;
            // 
            // pnlSharpeRatio
            // 
            pnlSharpeRatio.Controls.Add(lblAverageLoss);
            pnlSharpeRatio.Dock = DockStyle.Fill;
            pnlSharpeRatio.Location = new Point(345, 3);
            pnlSharpeRatio.Name = "pnlSharpeRatio";
            pnlSharpeRatio.Size = new Size(108, 24);
            pnlSharpeRatio.TabIndex = 6;
            // 
            // lblAverageLoss
            // 
            lblAverageLoss.AutoSize = true;
            lblAverageLoss.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblAverageLoss.ForeColor = Color.White;
            lblAverageLoss.Location = new Point(12, 2);
            lblAverageLoss.Name = "lblAverageLoss";
            lblAverageLoss.Size = new Size(71, 16);
            lblAverageLoss.TabIndex = 0;
            lblAverageLoss.Text = "Avg Loss";
            lblAverageLoss.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtAverageLoss
            // 
            txtAverageLoss.BackColor = Color.Black;
            txtAverageLoss.BorderStyle = BorderStyle.FixedSingle;
            txtAverageLoss.Dock = DockStyle.Fill;
            txtAverageLoss.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtAverageLoss.ForeColor = Color.White;
            txtAverageLoss.Location = new Point(345, 33);
            txtAverageLoss.Name = "txtAverageLoss";
            txtAverageLoss.Size = new Size(108, 22);
            txtAverageLoss.TabIndex = 7;
            txtAverageLoss.TextAlign = HorizontalAlignment.Center;
            // 
            // pnlCommission
            // 
            pnlCommission.Controls.Add(lblCommission);
            pnlCommission.Location = new Point(915, 3);
            pnlCommission.Name = "pnlCommission";
            pnlCommission.Size = new Size(107, 24);
            pnlCommission.TabIndex = 10;
            // 
            // lblCommission
            // 
            lblCommission.AutoSize = true;
            lblCommission.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCommission.ForeColor = Color.White;
            lblCommission.Location = new Point(13, 4);
            lblCommission.Name = "lblCommission";
            lblCommission.Size = new Size(91, 16);
            lblCommission.TabIndex = 0;
            lblCommission.Text = "Commission";
            lblCommission.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtCommission
            // 
            txtCommission.BackColor = Color.Black;
            txtCommission.BorderStyle = BorderStyle.FixedSingle;
            txtCommission.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtCommission.ForeColor = Color.White;
            txtCommission.Location = new Point(915, 33);
            txtCommission.Name = "txtCommission";
            txtCommission.Size = new Size(108, 22);
            txtCommission.TabIndex = 13;
            txtCommission.TextAlign = HorizontalAlignment.Center;
            // 
            // pnlProfitLossPercent
            // 
            pnlProfitLossPercent.Controls.Add(lblProfitLossPercent);
            pnlProfitLossPercent.Location = new Point(801, 3);
            pnlProfitLossPercent.Name = "pnlProfitLossPercent";
            pnlProfitLossPercent.Size = new Size(107, 24);
            pnlProfitLossPercent.TabIndex = 9;
            // 
            // lblProfitLossPercent
            // 
            lblProfitLossPercent.AutoSize = true;
            lblProfitLossPercent.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblProfitLossPercent.ForeColor = Color.White;
            lblProfitLossPercent.Location = new Point(25, 4);
            lblProfitLossPercent.Name = "lblProfitLossPercent";
            lblProfitLossPercent.Size = new Size(56, 16);
            lblProfitLossPercent.TabIndex = 0;
            lblProfitLossPercent.Text = "Pnl (%)";
            lblProfitLossPercent.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtProfitLossPercent
            // 
            txtProfitLossPercent.BackColor = Color.Black;
            txtProfitLossPercent.BorderStyle = BorderStyle.FixedSingle;
            txtProfitLossPercent.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtProfitLossPercent.ForeColor = Color.White;
            txtProfitLossPercent.Location = new Point(801, 33);
            txtProfitLossPercent.Name = "txtProfitLossPercent";
            txtProfitLossPercent.Size = new Size(107, 22);
            txtProfitLossPercent.TabIndex = 12;
            txtProfitLossPercent.TextAlign = HorizontalAlignment.Center;
            // 
            // pnlProfitLoss
            // 
            pnlProfitLoss.Controls.Add(lblProfitLoss);
            pnlProfitLoss.Location = new Point(687, 3);
            pnlProfitLoss.Name = "pnlProfitLoss";
            pnlProfitLoss.Size = new Size(107, 24);
            pnlProfitLoss.TabIndex = 8;
            // 
            // lblProfitLoss
            // 
            lblProfitLoss.AutoSize = true;
            lblProfitLoss.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblProfitLoss.ForeColor = Color.White;
            lblProfitLoss.Location = new Point(28, 4);
            lblProfitLoss.Name = "lblProfitLoss";
            lblProfitLoss.Size = new Size(51, 16);
            lblProfitLoss.TabIndex = 0;
            lblProfitLoss.Text = "Pnl ($)";
            lblProfitLoss.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtProfitLoss
            // 
            txtProfitLoss.BackColor = Color.Black;
            txtProfitLoss.BorderStyle = BorderStyle.FixedSingle;
            txtProfitLoss.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtProfitLoss.ForeColor = Color.White;
            txtProfitLoss.Location = new Point(687, 33);
            txtProfitLoss.Name = "txtProfitLoss";
            txtProfitLoss.Size = new Size(107, 22);
            txtProfitLoss.TabIndex = 11;
            txtProfitLoss.TextAlign = HorizontalAlignment.Center;
            // 
            // panel1
            // 
            panel1.Controls.Add(lblSharpeRatio);
            panel1.Location = new Point(573, 3);
            panel1.Name = "panel1";
            panel1.Size = new Size(107, 24);
            panel1.TabIndex = 14;
            // 
            // lblSharpeRatio
            // 
            lblSharpeRatio.AutoSize = true;
            lblSharpeRatio.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblSharpeRatio.ForeColor = Color.White;
            lblSharpeRatio.Location = new Point(5, 4);
            lblSharpeRatio.Name = "lblSharpeRatio";
            lblSharpeRatio.Size = new Size(98, 16);
            lblSharpeRatio.TabIndex = 0;
            lblSharpeRatio.Text = "Sharpe Ratio";
            lblSharpeRatio.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtSharpeRatio
            // 
            txtSharpeRatio.BackColor = Color.Black;
            txtSharpeRatio.BorderStyle = BorderStyle.FixedSingle;
            txtSharpeRatio.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtSharpeRatio.ForeColor = Color.White;
            txtSharpeRatio.Location = new Point(573, 33);
            txtSharpeRatio.Name = "txtSharpeRatio";
            txtSharpeRatio.Size = new Size(107, 22);
            txtSharpeRatio.TabIndex = 15;
            txtSharpeRatio.TextAlign = HorizontalAlignment.Center;
            // 
            // panel2
            // 
            panel2.Controls.Add(lblWinLossRatio);
            panel2.Location = new Point(459, 3);
            panel2.Name = "panel2";
            panel2.Size = new Size(107, 24);
            panel2.TabIndex = 16;
            // 
            // lblWinLossRatio
            // 
            lblWinLossRatio.AutoSize = true;
            lblWinLossRatio.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblWinLossRatio.ForeColor = Color.White;
            lblWinLossRatio.Location = new Point(18, 4);
            lblWinLossRatio.Name = "lblWinLossRatio";
            lblWinLossRatio.Size = new Size(75, 16);
            lblWinLossRatio.TabIndex = 0;
            lblWinLossRatio.Text = "W/L Ratio";
            lblWinLossRatio.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtWinLossRatio
            // 
            txtWinLossRatio.BackColor = Color.Black;
            txtWinLossRatio.BorderStyle = BorderStyle.FixedSingle;
            txtWinLossRatio.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtWinLossRatio.ForeColor = Color.White;
            txtWinLossRatio.Location = new Point(459, 33);
            txtWinLossRatio.Name = "txtWinLossRatio";
            txtWinLossRatio.Size = new Size(106, 22);
            txtWinLossRatio.TabIndex = 17;
            txtWinLossRatio.TextAlign = HorizontalAlignment.Center;
            // 
            // lblComment
            // 
            lblComment.AutoSize = true;
            lblComment.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblComment.ForeColor = Color.White;
            lblComment.Location = new Point(27, 591);
            lblComment.Margin = new Padding(2, 0, 2, 0);
            lblComment.Name = "lblComment";
            lblComment.Size = new Size(71, 17);
            lblComment.TabIndex = 12;
            lblComment.Text = "Comment:";
            // 
            // txtComment
            // 
            txtComment.BackColor = Color.Black;
            txtComment.BorderStyle = BorderStyle.FixedSingle;
            txtComment.Font = new Font("Microsoft Sans Serif", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtComment.ForeColor = Color.White;
            txtComment.Location = new Point(104, 591);
            txtComment.Margin = new Padding(2);
            txtComment.Multiline = true;
            txtComment.Name = "txtComment";
            txtComment.ReadOnly = true;
            txtComment.Size = new Size(1026, 70);
            txtComment.TabIndex = 11;
            // 
            // gridTransactions
            // 
            gridTransactions.AllowUserToDeleteRows = false;
            gridTransactions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            gridTransactions.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            gridTransactions.BackgroundColor = Color.Black;
            gridTransactions.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = Color.Black;
            dataGridViewCellStyle1.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle1.ForeColor = Color.White;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = Color.White;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            gridTransactions.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            gridTransactions.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = Color.Black;
            dataGridViewCellStyle2.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle2.ForeColor = Color.White;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = Color.White;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            gridTransactions.DefaultCellStyle = dataGridViewCellStyle2;
            gridTransactions.Location = new Point(104, 4);
            gridTransactions.Margin = new Padding(2);
            gridTransactions.MultiSelect = false;
            gridTransactions.Name = "gridTransactions";
            gridTransactions.ReadOnly = true;
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = Color.Black;
            dataGridViewCellStyle3.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle3.ForeColor = Color.White;
            dataGridViewCellStyle3.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = Color.White;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.True;
            gridTransactions.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            dataGridViewCellStyle4.BackColor = Color.Black;
            dataGridViewCellStyle4.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle4.ForeColor = Color.White;
            dataGridViewCellStyle4.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = Color.White;
            gridTransactions.RowsDefaultCellStyle = dataGridViewCellStyle4;
            gridTransactions.RowTemplate.Height = 24;
            gridTransactions.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridTransactions.Size = new Size(2067, 583);
            gridTransactions.TabIndex = 10;
            gridTransactions.CellContentClick += gridTransactions_CellContentClick;
            gridTransactions.SelectionChanged += gridTransactions_SelectionChanged;
            gridTransactions.DoubleClick += gridTransactions_DoubleClick;
            // 
            // lblTradeOrders
            // 
            lblTradeOrders.AutoSize = true;
            lblTradeOrders.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTradeOrders.ForeColor = Color.White;
            lblTradeOrders.Location = new Point(8, 6);
            lblTradeOrders.Margin = new Padding(2, 0, 2, 0);
            lblTradeOrders.Name = "lblTradeOrders";
            lblTradeOrders.Size = new Size(94, 17);
            lblTradeOrders.TabIndex = 1;
            lblTradeOrders.Text = "Transactions:";
            lblTradeOrders.TextAlign = ContentAlignment.MiddleRight;
            // 
            // fundTransactionsBindingSource
            // 
            fundTransactionsBindingSource.CurrentChanged += bindingSource1_CurrentChanged;
            // 
            // FundTransactionEditor
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(2176, 766);
            Controls.Add(pnlTradeOrders);
            Controls.Add(pnlFundSelector);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(2);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FundTransactionEditor";
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Fund Transactions Editor";
            FormClosed += FundTransactionEditor_FormClosed;
            Load += FundTransactionEditor_Load;
            pnlFundSelector.ResumeLayout(false);
            pnlFundSelector.PerformLayout();
            pnlTradeOrders.ResumeLayout(false);
            pnlTradeOrders.PerformLayout();
            tlpFundMetrics.ResumeLayout(false);
            tlpFundMetrics.PerformLayout();
            pnlWinRate.ResumeLayout(false);
            pnlWinRate.PerformLayout();
            pnlAverageProfit.ResumeLayout(false);
            pnlAverageProfit.PerformLayout();
            pnlAverageLoss.ResumeLayout(false);
            pnlAverageLoss.PerformLayout();
            pnlSharpeRatio.ResumeLayout(false);
            pnlSharpeRatio.PerformLayout();
            pnlCommission.ResumeLayout(false);
            pnlCommission.PerformLayout();
            pnlProfitLossPercent.ResumeLayout(false);
            pnlProfitLossPercent.PerformLayout();
            pnlProfitLoss.ResumeLayout(false);
            pnlProfitLoss.PerformLayout();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gridTransactions).EndInit();
            ((System.ComponentModel.ISupportInitialize)fundTransactionsBindingSource).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlFundSelector;
        private System.Windows.Forms.ComboBox ddlFund;
        private System.Windows.Forms.Label lblFundSelector;
        private System.Windows.Forms.Panel pnlTradeOrders;
        private System.Windows.Forms.DataGridView gridTransactions;
        private System.Windows.Forms.Label lblTradeOrders;
        private System.Windows.Forms.Label lblTo;
        private System.Windows.Forms.DateTimePicker dtpTo;
        private System.Windows.Forms.Label lblFrom;
        private System.Windows.Forms.DateTimePicker dtpFrom;
        private System.Windows.Forms.Label lblComment;
        private System.Windows.Forms.TextBox txtComment;
        private System.Windows.Forms.Label lblFundBalance;
        private System.Windows.Forms.TextBox txtFundBalance;
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
        private BindingSource fundTransactionsBindingSource;
        private Button btnAdjust;
    }
}