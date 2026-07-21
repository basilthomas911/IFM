namespace TomasAI.IFM.UI.Net.Views.App
{
    partial class MarketOutlookView
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
            tlpMarketOutlook = new TableLayoutPanel();
            lblVixVolRT = new Label();
            lblMarketTrendRT = new Label();
            txtVixVolRT = new TextBox();
            txtMarketDirectionRT = new TextBox();
            txtMarketTrendRT = new TextBox();
            lblMarketDirectionRT = new Label();
            txtMarketVolatilityRT = new TextBox();
            lblMarketVolatilityRt = new Label();
            tlpMarketData = new TableLayoutPanel();
            lblTdiStrength = new Label();
            lblMDIUpLimit = new Label();
            lblMDITrend = new Label();
            lblOpenRT = new Label();
            lblHighRT = new Label();
            lblLowRT = new Label();
            txtOpenRT = new TextBox();
            lblCloseRT = new Label();
            lblVolumeRT = new Label();
            lblPercentChangeRT = new Label();
            txtHighRT = new TextBox();
            txtLowRT = new TextBox();
            txtCloseRT = new TextBox();
            txtVolumeRT = new TextBox();
            txtPercentChangeRT = new TextBox();
            lblStdDevRT = new Label();
            lblUpperBandRT = new Label();
            lblMeanRT = new Label();
            lblLowerBandRT = new Label();
            lbl50Dma = new Label();
            lbl200Dma = new Label();
            txtStdDevRT = new TextBox();
            txtUpperBandRT = new TextBox();
            txtMeanRT = new TextBox();
            txtLowerBandRT = new TextBox();
            txt50DMA = new TextBox();
            txt200DMA = new TextBox();
            txtTrend = new TextBox();
            lblItiTrend = new Label();
            lblMdi = new Label();
            txtMDI = new TextBox();
            txtMDITrend = new TextBox();
            txtMDIUpLimit = new TextBox();
            txtMDIDownLimit = new TextBox();
            txtRSI = new TextBox();
            lblMDIDownLimit = new Label();
            tlpMarketTrendData = new TableLayoutPanel();
            txtTrendDelta = new TextBox();
            lblTrendDelta = new Label();
            lblTrendReversal = new Label();
            lblUpTrendLimit = new Label();
            txtReversalLimit = new TextBox();
            txtExtremeLimit = new TextBox();
            txtUpTrendLimit = new TextBox();
            lblTrendExtreme = new Label();
            txtDownTrendLimit = new TextBox();
            lblDownTrendLimit = new Label();
            tlpMarketOutlook.SuspendLayout();
            tlpMarketData.SuspendLayout();
            tlpMarketTrendData.SuspendLayout();
            SuspendLayout();
            // 
            // tlpMarketOutlook
            // 
            tlpMarketOutlook.BackColor = Color.Black;
            tlpMarketOutlook.BackgroundImageLayout = ImageLayout.None;
            tlpMarketOutlook.ColumnCount = 4;
            tlpMarketOutlook.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tlpMarketOutlook.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tlpMarketOutlook.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tlpMarketOutlook.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tlpMarketOutlook.Controls.Add(lblVixVolRT, 3, 0);
            tlpMarketOutlook.Controls.Add(lblMarketTrendRT, 0, 0);
            tlpMarketOutlook.Controls.Add(txtVixVolRT, 3, 1);
            tlpMarketOutlook.Controls.Add(txtMarketDirectionRT, 2, 1);
            tlpMarketOutlook.Controls.Add(txtMarketTrendRT, 0, 1);
            tlpMarketOutlook.Controls.Add(lblMarketDirectionRT, 2, 0);
            tlpMarketOutlook.Controls.Add(txtMarketVolatilityRT, 1, 1);
            tlpMarketOutlook.Controls.Add(lblMarketVolatilityRt, 1, 0);
            tlpMarketOutlook.Dock = DockStyle.Top;
            tlpMarketOutlook.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            tlpMarketOutlook.Location = new Point(0, 0);
            tlpMarketOutlook.Margin = new Padding(0);
            tlpMarketOutlook.Name = "tlpMarketOutlook";
            tlpMarketOutlook.RowCount = 2;
            tlpMarketOutlook.RowStyles.Add(new RowStyle(SizeType.Absolute, 23F));
            tlpMarketOutlook.RowStyles.Add(new RowStyle(SizeType.Absolute, 23F));
            tlpMarketOutlook.Size = new Size(621, 54);
            tlpMarketOutlook.TabIndex = 0;
            // 
            // lblVixVolRT
            // 
            lblVixVolRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblVixVolRT.AutoSize = true;
            lblVixVolRT.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblVixVolRT.ForeColor = Color.White;
            lblVixVolRT.Location = new Point(465, 7);
            lblVixVolRT.Margin = new Padding(0);
            lblVixVolRT.Name = "lblVixVolRT";
            lblVixVolRT.Size = new Size(127, 16);
            lblVixVolRT.TabIndex = 48;
            lblVixVolRT.Text = "     Price Volatility";
            lblVixVolRT.TextAlign = ContentAlignment.BottomCenter;
            // 
            // lblMarketTrendRT
            // 
            lblMarketTrendRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblMarketTrendRT.AutoSize = true;
            lblMarketTrendRT.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblMarketTrendRT.ForeColor = Color.White;
            lblMarketTrendRT.Location = new Point(0, 7);
            lblMarketTrendRT.Margin = new Padding(0);
            lblMarketTrendRT.Name = "lblMarketTrendRT";
            lblMarketTrendRT.Size = new Size(136, 16);
            lblMarketTrendRT.TabIndex = 45;
            lblMarketTrendRT.Text = "    Market Direction";
            lblMarketTrendRT.TextAlign = ContentAlignment.BottomCenter;
            lblMarketTrendRT.Click += lblMarketTrendRT_Click;
            // 
            // txtVixVolRT
            // 
            txtVixVolRT.BackColor = Color.Black;
            txtVixVolRT.BorderStyle = BorderStyle.FixedSingle;
            txtVixVolRT.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            txtVixVolRT.ForeColor = Color.White;
            txtVixVolRT.Location = new Point(467, 25);
            txtVixVolRT.Margin = new Padding(2);
            txtVixVolRT.Name = "txtVixVolRT";
            txtVixVolRT.ReadOnly = true;
            txtVixVolRT.Size = new Size(150, 24);
            txtVixVolRT.TabIndex = 44;
            txtVixVolRT.TextAlign = HorizontalAlignment.Center;
            // 
            // txtMarketDirectionRT
            // 
            txtMarketDirectionRT.BackColor = Color.Black;
            txtMarketDirectionRT.BorderStyle = BorderStyle.FixedSingle;
            txtMarketDirectionRT.Dock = DockStyle.Fill;
            txtMarketDirectionRT.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            txtMarketDirectionRT.ForeColor = Color.White;
            txtMarketDirectionRT.Location = new Point(312, 25);
            txtMarketDirectionRT.Margin = new Padding(2);
            txtMarketDirectionRT.Name = "txtMarketDirectionRT";
            txtMarketDirectionRT.ReadOnly = true;
            txtMarketDirectionRT.Size = new Size(151, 24);
            txtMarketDirectionRT.TabIndex = 43;
            txtMarketDirectionRT.TextAlign = HorizontalAlignment.Center;
            // 
            // txtMarketTrendRT
            // 
            txtMarketTrendRT.BackColor = Color.Black;
            txtMarketTrendRT.BorderStyle = BorderStyle.FixedSingle;
            txtMarketTrendRT.Dock = DockStyle.Top;
            txtMarketTrendRT.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtMarketTrendRT.ForeColor = Color.White;
            txtMarketTrendRT.Location = new Point(2, 25);
            txtMarketTrendRT.Margin = new Padding(2);
            txtMarketTrendRT.Name = "txtMarketTrendRT";
            txtMarketTrendRT.ReadOnly = true;
            txtMarketTrendRT.Size = new Size(151, 24);
            txtMarketTrendRT.TabIndex = 26;
            txtMarketTrendRT.TextAlign = HorizontalAlignment.Center;
            // 
            // lblMarketDirectionRT
            // 
            lblMarketDirectionRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblMarketDirectionRT.AutoSize = true;
            lblMarketDirectionRT.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblMarketDirectionRT.ForeColor = Color.White;
            lblMarketDirectionRT.Location = new Point(310, 7);
            lblMarketDirectionRT.Margin = new Padding(0);
            lblMarketDirectionRT.Name = "lblMarketDirectionRT";
            lblMarketDirectionRT.Size = new Size(137, 16);
            lblMarketDirectionRT.TabIndex = 47;
            lblMarketDirectionRT.Text = "       Price Direction";
            lblMarketDirectionRT.TextAlign = ContentAlignment.BottomCenter;
            // 
            // txtMarketVolatilityRT
            // 
            txtMarketVolatilityRT.BackColor = Color.Black;
            txtMarketVolatilityRT.BorderStyle = BorderStyle.FixedSingle;
            txtMarketVolatilityRT.Dock = DockStyle.Fill;
            txtMarketVolatilityRT.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtMarketVolatilityRT.ForeColor = Color.White;
            txtMarketVolatilityRT.Location = new Point(157, 25);
            txtMarketVolatilityRT.Margin = new Padding(2);
            txtMarketVolatilityRT.Name = "txtMarketVolatilityRT";
            txtMarketVolatilityRT.ReadOnly = true;
            txtMarketVolatilityRT.Size = new Size(151, 24);
            txtMarketVolatilityRT.TabIndex = 42;
            txtMarketVolatilityRT.Text = "   ";
            txtMarketVolatilityRT.TextAlign = HorizontalAlignment.Center;
            // 
            // lblMarketVolatilityRt
            // 
            lblMarketVolatilityRt.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblMarketVolatilityRt.AutoSize = true;
            lblMarketVolatilityRt.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblMarketVolatilityRt.ForeColor = Color.White;
            lblMarketVolatilityRt.Location = new Point(155, 7);
            lblMarketVolatilityRt.Margin = new Padding(0);
            lblMarketVolatilityRt.Name = "lblMarketVolatilityRt";
            lblMarketVolatilityRt.Size = new Size(134, 16);
            lblMarketVolatilityRt.TabIndex = 46;
            lblMarketVolatilityRt.Text = "    Market Volatility";
            lblMarketVolatilityRt.TextAlign = ContentAlignment.BottomCenter;
            // 
            // tlpMarketData
            // 
            tlpMarketData.BackColor = Color.Black;
            tlpMarketData.ColumnCount = 6;
            tlpMarketData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66667F));
            tlpMarketData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66666F));
            tlpMarketData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66666F));
            tlpMarketData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66666F));
            tlpMarketData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66666F));
            tlpMarketData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66666F));
            tlpMarketData.Controls.Add(lblTdiStrength, 5, 4);
            tlpMarketData.Controls.Add(lblMDIUpLimit, 3, 4);
            tlpMarketData.Controls.Add(lblMDITrend, 2, 4);
            tlpMarketData.Controls.Add(lblOpenRT, 0, 0);
            tlpMarketData.Controls.Add(lblHighRT, 1, 0);
            tlpMarketData.Controls.Add(lblLowRT, 2, 0);
            tlpMarketData.Controls.Add(txtOpenRT, 0, 1);
            tlpMarketData.Controls.Add(lblCloseRT, 3, 0);
            tlpMarketData.Controls.Add(lblVolumeRT, 4, 0);
            tlpMarketData.Controls.Add(lblPercentChangeRT, 5, 0);
            tlpMarketData.Controls.Add(txtHighRT, 1, 1);
            tlpMarketData.Controls.Add(txtLowRT, 2, 1);
            tlpMarketData.Controls.Add(txtCloseRT, 3, 1);
            tlpMarketData.Controls.Add(txtVolumeRT, 4, 1);
            tlpMarketData.Controls.Add(txtPercentChangeRT, 5, 1);
            tlpMarketData.Controls.Add(lblStdDevRT, 0, 2);
            tlpMarketData.Controls.Add(lblUpperBandRT, 1, 2);
            tlpMarketData.Controls.Add(lblMeanRT, 2, 2);
            tlpMarketData.Controls.Add(lblLowerBandRT, 3, 2);
            tlpMarketData.Controls.Add(lbl50Dma, 4, 2);
            tlpMarketData.Controls.Add(lbl200Dma, 5, 2);
            tlpMarketData.Controls.Add(txtStdDevRT, 0, 3);
            tlpMarketData.Controls.Add(txtUpperBandRT, 1, 3);
            tlpMarketData.Controls.Add(txtMeanRT, 2, 3);
            tlpMarketData.Controls.Add(txtLowerBandRT, 3, 3);
            tlpMarketData.Controls.Add(txt50DMA, 4, 3);
            tlpMarketData.Controls.Add(txt200DMA, 5, 3);
            tlpMarketData.Controls.Add(txtTrend, 0, 5);
            tlpMarketData.Controls.Add(lblItiTrend, 0, 4);
            tlpMarketData.Controls.Add(lblMdi, 1, 4);
            tlpMarketData.Controls.Add(txtMDI, 1, 5);
            tlpMarketData.Controls.Add(txtMDITrend, 2, 5);
            tlpMarketData.Controls.Add(txtMDIUpLimit, 3, 5);
            tlpMarketData.Controls.Add(txtMDIDownLimit, 4, 5);
            tlpMarketData.Controls.Add(txtRSI, 5, 5);
            tlpMarketData.Controls.Add(lblMDIDownLimit, 4, 4);
            tlpMarketData.Dock = DockStyle.Top;
            tlpMarketData.Location = new Point(0, 54);
            tlpMarketData.Margin = new Padding(4, 3, 4, 3);
            tlpMarketData.Name = "tlpMarketData";
            tlpMarketData.RowCount = 6;
            tlpMarketData.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66556F));
            tlpMarketData.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66889F));
            tlpMarketData.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66889F));
            tlpMarketData.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66556F));
            tlpMarketData.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66556F));
            tlpMarketData.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66556F));
            tlpMarketData.Size = new Size(621, 200);
            tlpMarketData.TabIndex = 1;
            // 
            // lblTdiStrength
            // 
            lblTdiStrength.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblTdiStrength.AutoSize = true;
            lblTdiStrength.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblTdiStrength.ForeColor = Color.White;
            lblTdiStrength.Location = new Point(519, 149);
            lblTdiStrength.Margin = new Padding(4, 0, 4, 0);
            lblTdiStrength.Name = "lblTdiStrength";
            lblTdiStrength.Size = new Size(64, 16);
            lblTdiStrength.TabIndex = 35;
            lblTdiStrength.Text = "        RSI";
            lblTdiStrength.TextAlign = ContentAlignment.BottomCenter;
            lblTdiStrength.Click += lblTdiStrength_Click;
            // 
            // lblMDIUpLimit
            // 
            lblMDIUpLimit.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblMDIUpLimit.AutoSize = true;
            lblMDIUpLimit.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblMDIUpLimit.ForeColor = Color.White;
            lblMDIUpLimit.Location = new Point(313, 149);
            lblMDIUpLimit.Margin = new Padding(4, 0, 4, 0);
            lblMDIUpLimit.Name = "lblMDIUpLimit";
            lblMDIUpLimit.Size = new Size(74, 16);
            lblMDIUpLimit.TabIndex = 33;
            lblMDIUpLimit.Text = "    MDI Up";
            // 
            // lblMDITrend
            // 
            lblMDITrend.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblMDITrend.AutoSize = true;
            lblMDITrend.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblMDITrend.ForeColor = Color.White;
            lblMDITrend.Location = new Point(210, 149);
            lblMDITrend.Margin = new Padding(4, 0, 4, 0);
            lblMDITrend.Name = "lblMDITrend";
            lblMDITrend.Size = new Size(79, 16);
            lblMDITrend.TabIndex = 32;
            lblMDITrend.Text = "MDI Trend";
            // 
            // lblOpenRT
            // 
            lblOpenRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblOpenRT.AutoSize = true;
            lblOpenRT.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblOpenRT.ForeColor = Color.White;
            lblOpenRT.Location = new Point(4, 16);
            lblOpenRT.Margin = new Padding(4, 0, 4, 0);
            lblOpenRT.Name = "lblOpenRT";
            lblOpenRT.Size = new Size(72, 17);
            lblOpenRT.TabIndex = 0;
            lblOpenRT.Text = "     Open";
            lblOpenRT.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblHighRT
            // 
            lblHighRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblHighRT.AutoSize = true;
            lblHighRT.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblHighRT.ForeColor = Color.White;
            lblHighRT.Location = new Point(107, 16);
            lblHighRT.Margin = new Padding(4, 0, 4, 0);
            lblHighRT.Name = "lblHighRT";
            lblHighRT.Size = new Size(71, 17);
            lblHighRT.TabIndex = 1;
            lblHighRT.Text = "      High";
            // 
            // lblLowRT
            // 
            lblLowRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblLowRT.AutoSize = true;
            lblLowRT.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblLowRT.ForeColor = Color.White;
            lblLowRT.Location = new Point(210, 16);
            lblLowRT.Margin = new Padding(4, 0, 4, 0);
            lblLowRT.Name = "lblLowRT";
            lblLowRT.Size = new Size(66, 17);
            lblLowRT.TabIndex = 2;
            lblLowRT.Text = "      Low";
            // 
            // txtOpenRT
            // 
            txtOpenRT.BackColor = Color.Black;
            txtOpenRT.BorderStyle = BorderStyle.FixedSingle;
            txtOpenRT.Dock = DockStyle.Fill;
            txtOpenRT.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtOpenRT.ForeColor = Color.White;
            txtOpenRT.Location = new Point(4, 36);
            txtOpenRT.Margin = new Padding(4, 3, 4, 3);
            txtOpenRT.Name = "txtOpenRT";
            txtOpenRT.Size = new Size(95, 20);
            txtOpenRT.TabIndex = 3;
            txtOpenRT.TextAlign = HorizontalAlignment.Center;
            // 
            // lblCloseRT
            // 
            lblCloseRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblCloseRT.AutoSize = true;
            lblCloseRT.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblCloseRT.ForeColor = Color.White;
            lblCloseRT.Location = new Point(313, 16);
            lblCloseRT.Margin = new Padding(4, 0, 4, 0);
            lblCloseRT.Name = "lblCloseRT";
            lblCloseRT.Size = new Size(73, 17);
            lblCloseRT.TabIndex = 4;
            lblCloseRT.Text = "     Close";
            // 
            // lblVolumeRT
            // 
            lblVolumeRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblVolumeRT.AutoSize = true;
            lblVolumeRT.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblVolumeRT.ForeColor = Color.White;
            lblVolumeRT.Location = new Point(416, 16);
            lblVolumeRT.Margin = new Padding(4, 0, 4, 0);
            lblVolumeRT.Name = "lblVolumeRT";
            lblVolumeRT.Size = new Size(76, 17);
            lblVolumeRT.TabIndex = 5;
            lblVolumeRT.Text = "   Volume";
            // 
            // lblPercentChangeRT
            // 
            lblPercentChangeRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblPercentChangeRT.AutoSize = true;
            lblPercentChangeRT.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblPercentChangeRT.ForeColor = Color.White;
            lblPercentChangeRT.Location = new Point(519, 16);
            lblPercentChangeRT.Margin = new Padding(4, 0, 4, 0);
            lblPercentChangeRT.Name = "lblPercentChangeRT";
            lblPercentChangeRT.Size = new Size(91, 17);
            lblPercentChangeRT.TabIndex = 6;
            lblPercentChangeRT.Text = "  % Change";
            // 
            // txtHighRT
            // 
            txtHighRT.BackColor = Color.Black;
            txtHighRT.BorderStyle = BorderStyle.FixedSingle;
            txtHighRT.Dock = DockStyle.Fill;
            txtHighRT.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtHighRT.ForeColor = Color.White;
            txtHighRT.Location = new Point(107, 36);
            txtHighRT.Margin = new Padding(4, 3, 4, 3);
            txtHighRT.Name = "txtHighRT";
            txtHighRT.Size = new Size(95, 20);
            txtHighRT.TabIndex = 7;
            txtHighRT.TextAlign = HorizontalAlignment.Center;
            // 
            // txtLowRT
            // 
            txtLowRT.BackColor = Color.Black;
            txtLowRT.BorderStyle = BorderStyle.FixedSingle;
            txtLowRT.Dock = DockStyle.Fill;
            txtLowRT.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtLowRT.ForeColor = Color.White;
            txtLowRT.Location = new Point(210, 36);
            txtLowRT.Margin = new Padding(4, 3, 4, 3);
            txtLowRT.Name = "txtLowRT";
            txtLowRT.Size = new Size(95, 20);
            txtLowRT.TabIndex = 8;
            txtLowRT.TextAlign = HorizontalAlignment.Center;
            // 
            // txtCloseRT
            // 
            txtCloseRT.BackColor = Color.Black;
            txtCloseRT.BorderStyle = BorderStyle.FixedSingle;
            txtCloseRT.Dock = DockStyle.Fill;
            txtCloseRT.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtCloseRT.ForeColor = Color.Yellow;
            txtCloseRT.Location = new Point(313, 36);
            txtCloseRT.Margin = new Padding(4, 3, 4, 3);
            txtCloseRT.Name = "txtCloseRT";
            txtCloseRT.Size = new Size(95, 20);
            txtCloseRT.TabIndex = 9;
            txtCloseRT.TextAlign = HorizontalAlignment.Center;
            // 
            // txtVolumeRT
            // 
            txtVolumeRT.BackColor = Color.Black;
            txtVolumeRT.BorderStyle = BorderStyle.FixedSingle;
            txtVolumeRT.Dock = DockStyle.Fill;
            txtVolumeRT.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtVolumeRT.ForeColor = Color.White;
            txtVolumeRT.Location = new Point(416, 36);
            txtVolumeRT.Margin = new Padding(4, 3, 4, 3);
            txtVolumeRT.Name = "txtVolumeRT";
            txtVolumeRT.Size = new Size(95, 20);
            txtVolumeRT.TabIndex = 10;
            txtVolumeRT.TextAlign = HorizontalAlignment.Center;
            // 
            // txtPercentChangeRT
            // 
            txtPercentChangeRT.BackColor = Color.Black;
            txtPercentChangeRT.BorderStyle = BorderStyle.FixedSingle;
            txtPercentChangeRT.Dock = DockStyle.Fill;
            txtPercentChangeRT.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtPercentChangeRT.ForeColor = Color.White;
            txtPercentChangeRT.Location = new Point(519, 36);
            txtPercentChangeRT.Margin = new Padding(4, 3, 4, 3);
            txtPercentChangeRT.Name = "txtPercentChangeRT";
            txtPercentChangeRT.Size = new Size(98, 20);
            txtPercentChangeRT.TabIndex = 11;
            txtPercentChangeRT.TextAlign = HorizontalAlignment.Center;
            // 
            // lblStdDevRT
            // 
            lblStdDevRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStdDevRT.AutoSize = true;
            lblStdDevRT.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblStdDevRT.ForeColor = Color.White;
            lblStdDevRT.Location = new Point(4, 82);
            lblStdDevRT.Margin = new Padding(4, 0, 4, 0);
            lblStdDevRT.Name = "lblStdDevRT";
            lblStdDevRT.Size = new Size(85, 17);
            lblStdDevRT.TabIndex = 12;
            lblStdDevRT.Text = "    Std Dev";
            lblStdDevRT.TextAlign = ContentAlignment.BottomCenter;
            // 
            // lblUpperBandRT
            // 
            lblUpperBandRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblUpperBandRT.AutoSize = true;
            lblUpperBandRT.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblUpperBandRT.ForeColor = Color.White;
            lblUpperBandRT.Location = new Point(107, 82);
            lblUpperBandRT.Margin = new Padding(4, 0, 4, 0);
            lblUpperBandRT.Name = "lblUpperBandRT";
            lblUpperBandRT.Size = new Size(82, 17);
            lblUpperBandRT.TabIndex = 13;
            lblUpperBandRT.Text = "     Upper ";
            // 
            // lblMeanRT
            // 
            lblMeanRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblMeanRT.AutoSize = true;
            lblMeanRT.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblMeanRT.ForeColor = Color.White;
            lblMeanRT.Location = new Point(210, 82);
            lblMeanRT.Margin = new Padding(4, 0, 4, 0);
            lblMeanRT.Name = "lblMeanRT";
            lblMeanRT.Size = new Size(77, 17);
            lblMeanRT.TabIndex = 14;
            lblMeanRT.Text = "      Mean";
            // 
            // lblLowerBandRT
            // 
            lblLowerBandRT.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblLowerBandRT.AutoSize = true;
            lblLowerBandRT.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblLowerBandRT.ForeColor = Color.White;
            lblLowerBandRT.Location = new Point(313, 82);
            lblLowerBandRT.Margin = new Padding(4, 0, 4, 0);
            lblLowerBandRT.Name = "lblLowerBandRT";
            lblLowerBandRT.Size = new Size(71, 17);
            lblLowerBandRT.TabIndex = 15;
            lblLowerBandRT.Text = "    Lower";
            // 
            // lbl50Dma
            // 
            lbl50Dma.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lbl50Dma.AutoSize = true;
            lbl50Dma.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lbl50Dma.ForeColor = Color.White;
            lbl50Dma.Location = new Point(416, 82);
            lbl50Dma.Margin = new Padding(4, 0, 4, 0);
            lbl50Dma.Name = "lbl50Dma";
            lbl50Dma.Size = new Size(79, 17);
            lbl50Dma.TabIndex = 16;
            lbl50Dma.Text = "   50 DMA";
            // 
            // lbl200Dma
            // 
            lbl200Dma.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lbl200Dma.AutoSize = true;
            lbl200Dma.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lbl200Dma.ForeColor = Color.White;
            lbl200Dma.Location = new Point(519, 82);
            lbl200Dma.Margin = new Padding(4, 0, 4, 0);
            lbl200Dma.Name = "lbl200Dma";
            lbl200Dma.Size = new Size(83, 17);
            lbl200Dma.TabIndex = 17;
            lbl200Dma.Text = "  200 DMA";
            lbl200Dma.Click += lblRiskPosition_Click;
            // 
            // txtStdDevRT
            // 
            txtStdDevRT.BackColor = Color.Black;
            txtStdDevRT.BorderStyle = BorderStyle.FixedSingle;
            txtStdDevRT.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtStdDevRT.ForeColor = Color.White;
            txtStdDevRT.Location = new Point(4, 102);
            txtStdDevRT.Margin = new Padding(4, 3, 4, 3);
            txtStdDevRT.Name = "txtStdDevRT";
            txtStdDevRT.Size = new Size(95, 20);
            txtStdDevRT.TabIndex = 18;
            txtStdDevRT.TextAlign = HorizontalAlignment.Center;
            // 
            // txtUpperBandRT
            // 
            txtUpperBandRT.BackColor = Color.Black;
            txtUpperBandRT.BorderStyle = BorderStyle.FixedSingle;
            txtUpperBandRT.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtUpperBandRT.ForeColor = Color.White;
            txtUpperBandRT.Location = new Point(107, 102);
            txtUpperBandRT.Margin = new Padding(4, 3, 4, 3);
            txtUpperBandRT.Name = "txtUpperBandRT";
            txtUpperBandRT.Size = new Size(95, 20);
            txtUpperBandRT.TabIndex = 19;
            txtUpperBandRT.TextAlign = HorizontalAlignment.Center;
            // 
            // txtMeanRT
            // 
            txtMeanRT.BackColor = Color.Black;
            txtMeanRT.BorderStyle = BorderStyle.FixedSingle;
            txtMeanRT.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtMeanRT.ForeColor = Color.White;
            txtMeanRT.Location = new Point(210, 102);
            txtMeanRT.Margin = new Padding(4, 3, 4, 3);
            txtMeanRT.Name = "txtMeanRT";
            txtMeanRT.Size = new Size(95, 20);
            txtMeanRT.TabIndex = 20;
            txtMeanRT.TextAlign = HorizontalAlignment.Center;
            // 
            // txtLowerBandRT
            // 
            txtLowerBandRT.BackColor = Color.Black;
            txtLowerBandRT.BorderStyle = BorderStyle.FixedSingle;
            txtLowerBandRT.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtLowerBandRT.ForeColor = Color.White;
            txtLowerBandRT.Location = new Point(313, 102);
            txtLowerBandRT.Margin = new Padding(4, 3, 4, 3);
            txtLowerBandRT.Name = "txtLowerBandRT";
            txtLowerBandRT.Size = new Size(95, 20);
            txtLowerBandRT.TabIndex = 21;
            txtLowerBandRT.TextAlign = HorizontalAlignment.Center;
            // 
            // txt50DMA
            // 
            txt50DMA.BackColor = Color.Black;
            txt50DMA.BorderStyle = BorderStyle.FixedSingle;
            txt50DMA.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txt50DMA.ForeColor = Color.White;
            txt50DMA.Location = new Point(416, 102);
            txt50DMA.Margin = new Padding(4, 3, 4, 3);
            txt50DMA.Name = "txt50DMA";
            txt50DMA.Size = new Size(95, 20);
            txt50DMA.TabIndex = 22;
            txt50DMA.TextAlign = HorizontalAlignment.Center;
            // 
            // txt200DMA
            // 
            txt200DMA.BackColor = Color.Black;
            txt200DMA.BorderStyle = BorderStyle.FixedSingle;
            txt200DMA.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txt200DMA.ForeColor = Color.White;
            txt200DMA.Location = new Point(519, 102);
            txt200DMA.Margin = new Padding(4, 3, 4, 3);
            txt200DMA.Name = "txt200DMA";
            txt200DMA.Size = new Size(98, 20);
            txt200DMA.TabIndex = 23;
            txt200DMA.TextAlign = HorizontalAlignment.Center;
            txt200DMA.TextChanged += txt200DMA_TextChanged;
            // 
            // txtTrend
            // 
            txtTrend.BackColor = Color.Black;
            txtTrend.BorderStyle = BorderStyle.FixedSingle;
            txtTrend.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtTrend.ForeColor = Color.White;
            txtTrend.Location = new Point(4, 168);
            txtTrend.Margin = new Padding(4, 3, 4, 3);
            txtTrend.Name = "txtTrend";
            txtTrend.Size = new Size(95, 20);
            txtTrend.TabIndex = 24;
            txtTrend.TextAlign = HorizontalAlignment.Center;
            // 
            // lblItiTrend
            // 
            lblItiTrend.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblItiTrend.AutoSize = true;
            lblItiTrend.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblItiTrend.ForeColor = Color.White;
            lblItiTrend.Location = new Point(4, 149);
            lblItiTrend.Margin = new Padding(4, 0, 4, 0);
            lblItiTrend.Name = "lblItiTrend";
            lblItiTrend.Size = new Size(86, 16);
            lblItiTrend.TabIndex = 25;
            lblItiTrend.Text = "    ITI Trend";
            lblItiTrend.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblMdi
            // 
            lblMdi.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblMdi.AutoSize = true;
            lblMdi.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblMdi.ForeColor = Color.White;
            lblMdi.Location = new Point(107, 149);
            lblMdi.Margin = new Padding(4, 0, 4, 0);
            lblMdi.Name = "lblMdi";
            lblMdi.Size = new Size(54, 16);
            lblMdi.TabIndex = 26;
            lblMdi.Text = "     MDI";
            // 
            // txtMDI
            // 
            txtMDI.BackColor = Color.Black;
            txtMDI.BorderStyle = BorderStyle.FixedSingle;
            txtMDI.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtMDI.ForeColor = Color.White;
            txtMDI.Location = new Point(107, 168);
            txtMDI.Margin = new Padding(4, 3, 4, 3);
            txtMDI.Name = "txtMDI";
            txtMDI.Size = new Size(95, 20);
            txtMDI.TabIndex = 27;
            txtMDI.TextAlign = HorizontalAlignment.Center;
            // 
            // txtMDITrend
            // 
            txtMDITrend.BackColor = Color.Black;
            txtMDITrend.BorderStyle = BorderStyle.FixedSingle;
            txtMDITrend.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtMDITrend.ForeColor = Color.White;
            txtMDITrend.Location = new Point(210, 168);
            txtMDITrend.Margin = new Padding(4, 3, 4, 3);
            txtMDITrend.Name = "txtMDITrend";
            txtMDITrend.Size = new Size(95, 20);
            txtMDITrend.TabIndex = 28;
            txtMDITrend.TextAlign = HorizontalAlignment.Center;
            // 
            // txtMDIUpLimit
            // 
            txtMDIUpLimit.BackColor = Color.Black;
            txtMDIUpLimit.BorderStyle = BorderStyle.FixedSingle;
            txtMDIUpLimit.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtMDIUpLimit.ForeColor = Color.White;
            txtMDIUpLimit.Location = new Point(313, 168);
            txtMDIUpLimit.Margin = new Padding(4, 3, 4, 3);
            txtMDIUpLimit.Name = "txtMDIUpLimit";
            txtMDIUpLimit.Size = new Size(95, 20);
            txtMDIUpLimit.TabIndex = 29;
            txtMDIUpLimit.TextAlign = HorizontalAlignment.Center;
            // 
            // txtMDIDownLimit
            // 
            txtMDIDownLimit.BackColor = Color.Black;
            txtMDIDownLimit.BorderStyle = BorderStyle.FixedSingle;
            txtMDIDownLimit.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtMDIDownLimit.ForeColor = Color.White;
            txtMDIDownLimit.Location = new Point(416, 168);
            txtMDIDownLimit.Margin = new Padding(4, 3, 4, 3);
            txtMDIDownLimit.Name = "txtMDIDownLimit";
            txtMDIDownLimit.Size = new Size(95, 20);
            txtMDIDownLimit.TabIndex = 30;
            txtMDIDownLimit.TextAlign = HorizontalAlignment.Center;
            // 
            // txtRSI
            // 
            txtRSI.BackColor = Color.Black;
            txtRSI.BorderStyle = BorderStyle.FixedSingle;
            txtRSI.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtRSI.ForeColor = Color.White;
            txtRSI.Location = new Point(519, 168);
            txtRSI.Margin = new Padding(4, 3, 4, 3);
            txtRSI.Name = "txtRSI";
            txtRSI.Size = new Size(95, 20);
            txtRSI.TabIndex = 31;
            txtRSI.TextAlign = HorizontalAlignment.Center;
            // 
            // lblMDIDownLimit
            // 
            lblMDIDownLimit.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblMDIDownLimit.AutoSize = true;
            lblMDIDownLimit.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblMDIDownLimit.ForeColor = Color.White;
            lblMDIDownLimit.Location = new Point(416, 149);
            lblMDIDownLimit.Margin = new Padding(4, 0, 4, 0);
            lblMDIDownLimit.Name = "lblMDIDownLimit";
            lblMDIDownLimit.Size = new Size(80, 16);
            lblMDIDownLimit.TabIndex = 34;
            lblMDIDownLimit.Text = " MDI Down";
            // 
            // tlpMarketTrendData
            // 
            tlpMarketTrendData.BackColor = Color.Black;
            tlpMarketTrendData.BackgroundImageLayout = ImageLayout.None;
            tlpMarketTrendData.ColumnCount = 5;
            tlpMarketTrendData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tlpMarketTrendData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tlpMarketTrendData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tlpMarketTrendData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tlpMarketTrendData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tlpMarketTrendData.Controls.Add(txtTrendDelta, 4, 1);
            tlpMarketTrendData.Controls.Add(lblTrendDelta, 4, 0);
            tlpMarketTrendData.Controls.Add(lblTrendReversal, 3, 0);
            tlpMarketTrendData.Controls.Add(lblUpTrendLimit, 0, 0);
            tlpMarketTrendData.Controls.Add(txtReversalLimit, 3, 1);
            tlpMarketTrendData.Controls.Add(txtExtremeLimit, 2, 1);
            tlpMarketTrendData.Controls.Add(txtUpTrendLimit, 0, 1);
            tlpMarketTrendData.Controls.Add(lblTrendExtreme, 2, 0);
            tlpMarketTrendData.Controls.Add(txtDownTrendLimit, 1, 1);
            tlpMarketTrendData.Controls.Add(lblDownTrendLimit, 1, 0);
            tlpMarketTrendData.Dock = DockStyle.Top;
            tlpMarketTrendData.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            tlpMarketTrendData.Location = new Point(0, 254);
            tlpMarketTrendData.Margin = new Padding(0);
            tlpMarketTrendData.Name = "tlpMarketTrendData";
            tlpMarketTrendData.RowCount = 2;
            tlpMarketTrendData.RowStyles.Add(new RowStyle(SizeType.Absolute, 23F));
            tlpMarketTrendData.RowStyles.Add(new RowStyle(SizeType.Absolute, 23F));
            tlpMarketTrendData.Size = new Size(621, 54);
            tlpMarketTrendData.TabIndex = 2;
            // 
            // txtTrendDelta
            // 
            txtTrendDelta.BackColor = Color.Black;
            txtTrendDelta.BorderStyle = BorderStyle.FixedSingle;
            txtTrendDelta.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            txtTrendDelta.ForeColor = Color.White;
            txtTrendDelta.Location = new Point(498, 25);
            txtTrendDelta.Margin = new Padding(2);
            txtTrendDelta.Name = "txtTrendDelta";
            txtTrendDelta.ReadOnly = true;
            txtTrendDelta.Size = new Size(119, 24);
            txtTrendDelta.TabIndex = 50;
            txtTrendDelta.TextAlign = HorizontalAlignment.Center;
            // 
            // lblTrendDelta
            // 
            lblTrendDelta.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblTrendDelta.AutoSize = true;
            lblTrendDelta.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblTrendDelta.ForeColor = Color.White;
            lblTrendDelta.Location = new Point(496, 8);
            lblTrendDelta.Margin = new Padding(0);
            lblTrendDelta.Name = "lblTrendDelta";
            lblTrendDelta.Size = new Size(98, 15);
            lblTrendDelta.TabIndex = 49;
            lblTrendDelta.Text = "    Trend Delta";
            lblTrendDelta.TextAlign = ContentAlignment.BottomCenter;
            // 
            // lblTrendReversal
            // 
            lblTrendReversal.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblTrendReversal.AutoSize = true;
            lblTrendReversal.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblTrendReversal.ForeColor = Color.White;
            lblTrendReversal.Location = new Point(372, 8);
            lblTrendReversal.Margin = new Padding(0);
            lblTrendReversal.Name = "lblTrendReversal";
            lblTrendReversal.Size = new Size(108, 15);
            lblTrendReversal.TabIndex = 48;
            lblTrendReversal.Text = " Trend Reversal";
            lblTrendReversal.TextAlign = ContentAlignment.BottomCenter;
            // 
            // lblUpTrendLimit
            // 
            lblUpTrendLimit.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblUpTrendLimit.AutoSize = true;
            lblUpTrendLimit.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblUpTrendLimit.ForeColor = Color.White;
            lblUpTrendLimit.Location = new Point(0, 8);
            lblUpTrendLimit.Margin = new Padding(0);
            lblUpTrendLimit.Name = "lblUpTrendLimit";
            lblUpTrendLimit.Size = new Size(110, 15);
            lblUpTrendLimit.TabIndex = 45;
            lblUpTrendLimit.Text = "  UpTrend Limit ";
            lblUpTrendLimit.TextAlign = ContentAlignment.BottomCenter;
            // 
            // txtReversalLimit
            // 
            txtReversalLimit.BackColor = Color.Black;
            txtReversalLimit.BorderStyle = BorderStyle.FixedSingle;
            txtReversalLimit.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            txtReversalLimit.ForeColor = Color.White;
            txtReversalLimit.Location = new Point(374, 25);
            txtReversalLimit.Margin = new Padding(2);
            txtReversalLimit.Name = "txtReversalLimit";
            txtReversalLimit.ReadOnly = true;
            txtReversalLimit.Size = new Size(119, 24);
            txtReversalLimit.TabIndex = 44;
            txtReversalLimit.TextAlign = HorizontalAlignment.Center;
            // 
            // txtExtremeLimit
            // 
            txtExtremeLimit.BackColor = Color.Black;
            txtExtremeLimit.BorderStyle = BorderStyle.FixedSingle;
            txtExtremeLimit.Dock = DockStyle.Fill;
            txtExtremeLimit.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            txtExtremeLimit.ForeColor = Color.White;
            txtExtremeLimit.Location = new Point(250, 25);
            txtExtremeLimit.Margin = new Padding(2);
            txtExtremeLimit.Name = "txtExtremeLimit";
            txtExtremeLimit.ReadOnly = true;
            txtExtremeLimit.Size = new Size(120, 24);
            txtExtremeLimit.TabIndex = 43;
            txtExtremeLimit.TextAlign = HorizontalAlignment.Center;
            // 
            // txtUpTrendLimit
            // 
            txtUpTrendLimit.BackColor = Color.Black;
            txtUpTrendLimit.BorderStyle = BorderStyle.FixedSingle;
            txtUpTrendLimit.Dock = DockStyle.Top;
            txtUpTrendLimit.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtUpTrendLimit.ForeColor = Color.White;
            txtUpTrendLimit.Location = new Point(2, 25);
            txtUpTrendLimit.Margin = new Padding(2);
            txtUpTrendLimit.Name = "txtUpTrendLimit";
            txtUpTrendLimit.ReadOnly = true;
            txtUpTrendLimit.Size = new Size(120, 24);
            txtUpTrendLimit.TabIndex = 26;
            txtUpTrendLimit.TextAlign = HorizontalAlignment.Center;
            // 
            // lblTrendExtreme
            // 
            lblTrendExtreme.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblTrendExtreme.AutoSize = true;
            lblTrendExtreme.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblTrendExtreme.ForeColor = Color.White;
            lblTrendExtreme.Location = new Point(248, 8);
            lblTrendExtreme.Margin = new Padding(0);
            lblTrendExtreme.Name = "lblTrendExtreme";
            lblTrendExtreme.Size = new Size(109, 15);
            lblTrendExtreme.TabIndex = 47;
            lblTrendExtreme.Text = "  Trend Extreme";
            lblTrendExtreme.TextAlign = ContentAlignment.BottomCenter;
            // 
            // txtDownTrendLimit
            // 
            txtDownTrendLimit.BackColor = Color.Black;
            txtDownTrendLimit.BorderStyle = BorderStyle.FixedSingle;
            txtDownTrendLimit.Dock = DockStyle.Fill;
            txtDownTrendLimit.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
            txtDownTrendLimit.ForeColor = Color.White;
            txtDownTrendLimit.Location = new Point(126, 25);
            txtDownTrendLimit.Margin = new Padding(2);
            txtDownTrendLimit.Name = "txtDownTrendLimit";
            txtDownTrendLimit.ReadOnly = true;
            txtDownTrendLimit.Size = new Size(120, 24);
            txtDownTrendLimit.TabIndex = 42;
            txtDownTrendLimit.Text = "   ";
            txtDownTrendLimit.TextAlign = HorizontalAlignment.Center;
            // 
            // lblDownTrendLimit
            // 
            lblDownTrendLimit.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblDownTrendLimit.AutoSize = true;
            lblDownTrendLimit.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblDownTrendLimit.ForeColor = Color.White;
            lblDownTrendLimit.Location = new Point(124, 8);
            lblDownTrendLimit.Margin = new Padding(0);
            lblDownTrendLimit.Name = "lblDownTrendLimit";
            lblDownTrendLimit.Size = new Size(120, 15);
            lblDownTrendLimit.TabIndex = 46;
            lblDownTrendLimit.Text = " DownTrend Limit";
            lblDownTrendLimit.TextAlign = ContentAlignment.BottomCenter;
            // 
            // MarketOutlookView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Black;
            Controls.Add(tlpMarketTrendData);
            Controls.Add(tlpMarketData);
            Controls.Add(tlpMarketOutlook);
            Margin = new Padding(4, 3, 4, 3);
            Name = "MarketOutlookView";
            Size = new Size(621, 310);
            tlpMarketOutlook.ResumeLayout(false);
            tlpMarketOutlook.PerformLayout();
            tlpMarketData.ResumeLayout(false);
            tlpMarketData.PerformLayout();
            tlpMarketTrendData.ResumeLayout(false);
            tlpMarketTrendData.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpMarketOutlook;
        private System.Windows.Forms.TextBox txtMarketTrendRT;
        private System.Windows.Forms.TextBox txtMarketVolatilityRT;
        private System.Windows.Forms.TextBox txtMarketDirectionRT;
        private System.Windows.Forms.TextBox txtVixVolRT;
        private System.Windows.Forms.TableLayoutPanel tlpMarketData;
        private System.Windows.Forms.Label lblOpenRT;
        private System.Windows.Forms.Label lblHighRT;
        private System.Windows.Forms.Label lblLowRT;
        private System.Windows.Forms.TextBox txtOpenRT;
        private System.Windows.Forms.Label lblCloseRT;
        private System.Windows.Forms.Label lblVolumeRT;
        private System.Windows.Forms.Label lblPercentChangeRT;
        private System.Windows.Forms.TextBox txtHighRT;
        private System.Windows.Forms.TextBox txtLowRT;
        private System.Windows.Forms.TextBox txtCloseRT;
        private System.Windows.Forms.TextBox txtVolumeRT;
        private System.Windows.Forms.TextBox txtPercentChangeRT;
        private System.Windows.Forms.Label lblStdDevRT;
        private System.Windows.Forms.Label lblUpperBandRT;
        private System.Windows.Forms.Label lblMeanRT;
        private System.Windows.Forms.Label lblLowerBandRT;
        private System.Windows.Forms.Label lbl50Dma;
        private System.Windows.Forms.Label lbl200Dma;
        private System.Windows.Forms.TextBox txtStdDevRT;
        private System.Windows.Forms.TextBox txtUpperBandRT;
        private System.Windows.Forms.TextBox txtMeanRT;
        private System.Windows.Forms.TextBox txtLowerBandRT;
        private System.Windows.Forms.TextBox txt50DMA;
        private System.Windows.Forms.TextBox txt200DMA;
        private System.Windows.Forms.Label lblVixVolRT;
        private System.Windows.Forms.Label lblMarketTrendRT;
        private System.Windows.Forms.Label lblMarketDirectionRT;
        private System.Windows.Forms.Label lblMarketVolatilityRt;
        private System.Windows.Forms.TextBox txtTrend;
        private System.Windows.Forms.Label lblItiTrend;
        private System.Windows.Forms.Label lblTdiStrength;
        private System.Windows.Forms.Label lblMDIDownLimit;
        private System.Windows.Forms.Label lblMDIUpLimit;
        private System.Windows.Forms.Label lblMDITrend;
        private System.Windows.Forms.Label lblMdi;
        private System.Windows.Forms.TextBox txtMDI;
        private System.Windows.Forms.TextBox txtMDITrend;
        private System.Windows.Forms.TextBox txtMDIUpLimit;
        private System.Windows.Forms.TextBox txtMDIDownLimit;
        private System.Windows.Forms.TextBox txtRSI;
        private System.Windows.Forms.TableLayoutPanel tlpMarketTrendData;
        private System.Windows.Forms.Label lblTrendReversal;
        private System.Windows.Forms.Label lblUpTrendLimit;
        private System.Windows.Forms.TextBox txtReversalLimit;
        private System.Windows.Forms.TextBox txtExtremeLimit;
        private System.Windows.Forms.TextBox txtUpTrendLimit;
        private System.Windows.Forms.Label lblTrendExtreme;
        private System.Windows.Forms.TextBox txtDownTrendLimit;
        private System.Windows.Forms.Label lblDownTrendLimit;
        private System.Windows.Forms.TextBox txtTrendDelta;
        private System.Windows.Forms.Label lblTrendDelta;
    }
}
