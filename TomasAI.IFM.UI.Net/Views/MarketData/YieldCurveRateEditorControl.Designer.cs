namespace TomasAI.IFM.Views.MarketData
{
    partial class YieldCurveRateEditorControl
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
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle15 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle16 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle6 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle7 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle8 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle9 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle10 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle11 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle12 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle13 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle14 = new DataGridViewCellStyle();
            pnlTimePeriod = new Panel();
            ddlTimePeriod = new ComboBox();
            lblTimePeriod = new Label();
            yieldCurveRatesBindingSource = new BindingSource(components);
            gridYieldCurveRates = new DataGridView();
            valueDateDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            oneMonthDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            TwoMonth = new DataGridViewTextBoxColumn();
            threeMonthDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            sixMonthDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            oneYearDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            twoYearDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            threeYearDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            fiveYearDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            sevenYearDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            tenYearDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            twentyYearDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            thirtyYearDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            pnlTimePeriod.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)yieldCurveRatesBindingSource).BeginInit();
            ((System.ComponentModel.ISupportInitialize)gridYieldCurveRates).BeginInit();
            SuspendLayout();
            // 
            // pnlTimePeriod
            // 
            pnlTimePeriod.BackColor = Color.FromArgb(64, 64, 64);
            pnlTimePeriod.Controls.Add(ddlTimePeriod);
            pnlTimePeriod.Controls.Add(lblTimePeriod);
            pnlTimePeriod.Dock = DockStyle.Top;
            pnlTimePeriod.Location = new Point(0, 0);
            pnlTimePeriod.Margin = new Padding(2);
            pnlTimePeriod.Name = "pnlTimePeriod";
            pnlTimePeriod.Size = new Size(1100, 32);
            pnlTimePeriod.TabIndex = 0;
            // 
            // ddlTimePeriod
            // 
            ddlTimePeriod.DropDownStyle = ComboBoxStyle.DropDownList;
            ddlTimePeriod.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ddlTimePeriod.FormattingEnabled = true;
            ddlTimePeriod.Location = new Point(121, 2);
            ddlTimePeriod.Margin = new Padding(2);
            ddlTimePeriod.Name = "ddlTimePeriod";
            ddlTimePeriod.Size = new Size(248, 24);
            ddlTimePeriod.TabIndex = 1;
            ddlTimePeriod.SelectedIndexChanged += ddlTimePeriod_SelectedIndexChanged;
            // 
            // lblTimePeriod
            // 
            lblTimePeriod.AutoSize = true;
            lblTimePeriod.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTimePeriod.ForeColor = Color.White;
            lblTimePeriod.Location = new Point(14, 6);
            lblTimePeriod.Margin = new Padding(2, 0, 2, 0);
            lblTimePeriod.Name = "lblTimePeriod";
            lblTimePeriod.Size = new Size(88, 17);
            lblTimePeriod.TabIndex = 0;
            lblTimePeriod.Text = "Time Period:";
            lblTimePeriod.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // yieldCurveRatesBindingSource
            // 
            yieldCurveRatesBindingSource.DataSource = typeof(Shared.MarketData.ViewModels.YieldCurveRateReadModel);
            // 
            // gridYieldCurveRates
            // 
            gridYieldCurveRates.AllowUserToAddRows = false;
            gridYieldCurveRates.AllowUserToDeleteRows = false;
            gridYieldCurveRates.AllowUserToResizeRows = false;
            gridYieldCurveRates.AutoGenerateColumns = false;
            gridYieldCurveRates.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            gridYieldCurveRates.BackgroundColor = Color.FromArgb(64, 64, 64);
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(64, 64, 64);
            dataGridViewCellStyle1.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle1.ForeColor = Color.White;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = Color.White;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            gridYieldCurveRates.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            gridYieldCurveRates.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridYieldCurveRates.Columns.AddRange(new DataGridViewColumn[] { valueDateDataGridViewTextBoxColumn, oneMonthDataGridViewTextBoxColumn, TwoMonth, threeMonthDataGridViewTextBoxColumn, sixMonthDataGridViewTextBoxColumn, oneYearDataGridViewTextBoxColumn, twoYearDataGridViewTextBoxColumn, threeYearDataGridViewTextBoxColumn, fiveYearDataGridViewTextBoxColumn, sevenYearDataGridViewTextBoxColumn, tenYearDataGridViewTextBoxColumn, twentyYearDataGridViewTextBoxColumn, thirtyYearDataGridViewTextBoxColumn });
            gridYieldCurveRates.DataSource = yieldCurveRatesBindingSource;
            dataGridViewCellStyle15.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle15.BackColor = SystemColors.ControlDarkDark;
            dataGridViewCellStyle15.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle15.ForeColor = Color.White;
            dataGridViewCellStyle15.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle15.SelectionForeColor = Color.White;
            dataGridViewCellStyle15.WrapMode = DataGridViewTriState.False;
            gridYieldCurveRates.DefaultCellStyle = dataGridViewCellStyle15;
            gridYieldCurveRates.Dock = DockStyle.Fill;
            gridYieldCurveRates.EditMode = DataGridViewEditMode.EditProgrammatically;
            gridYieldCurveRates.Location = new Point(0, 32);
            gridYieldCurveRates.Margin = new Padding(2);
            gridYieldCurveRates.MultiSelect = false;
            gridYieldCurveRates.Name = "gridYieldCurveRates";
            gridYieldCurveRates.ReadOnly = true;
            dataGridViewCellStyle16.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle16.BackColor = SystemColors.Control;
            dataGridViewCellStyle16.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle16.ForeColor = Color.White;
            dataGridViewCellStyle16.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle16.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle16.WrapMode = DataGridViewTriState.True;
            gridYieldCurveRates.RowHeadersDefaultCellStyle = dataGridViewCellStyle16;
            gridYieldCurveRates.RowTemplate.Height = 28;
            gridYieldCurveRates.ScrollBars = ScrollBars.Vertical;
            gridYieldCurveRates.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridYieldCurveRates.Size = new Size(1100, 436);
            gridYieldCurveRates.TabIndex = 3;
            // 
            // valueDateDataGridViewTextBoxColumn
            // 
            valueDateDataGridViewTextBoxColumn.DataPropertyName = "ValueDate";
            dataGridViewCellStyle2.Format = "yyyy-MM-dd";
            valueDateDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle2;
            valueDateDataGridViewTextBoxColumn.HeaderText = "Date";
            valueDateDataGridViewTextBoxColumn.Name = "valueDateDataGridViewTextBoxColumn";
            valueDateDataGridViewTextBoxColumn.ReadOnly = true;
            valueDateDataGridViewTextBoxColumn.Width = 180;
            // 
            // oneMonthDataGridViewTextBoxColumn
            // 
            oneMonthDataGridViewTextBoxColumn.DataPropertyName = "OneMonth";
            dataGridViewCellStyle3.Format = "N2";
            dataGridViewCellStyle3.NullValue = null;
            oneMonthDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle3;
            oneMonthDataGridViewTextBoxColumn.HeaderText = "1 Mo";
            oneMonthDataGridViewTextBoxColumn.Name = "oneMonthDataGridViewTextBoxColumn";
            oneMonthDataGridViewTextBoxColumn.ReadOnly = true;
            oneMonthDataGridViewTextBoxColumn.Width = 60;
            // 
            // TwoMonth
            // 
            TwoMonth.DataPropertyName = "TwoMonth";
            dataGridViewCellStyle4.Format = "N2";
            TwoMonth.DefaultCellStyle = dataGridViewCellStyle4;
            TwoMonth.HeaderText = "2 Mo";
            TwoMonth.Name = "TwoMonth";
            TwoMonth.ReadOnly = true;
            TwoMonth.Width = 60;
            // 
            // threeMonthDataGridViewTextBoxColumn
            // 
            threeMonthDataGridViewTextBoxColumn.DataPropertyName = "ThreeMonth";
            dataGridViewCellStyle5.Format = "N2";
            threeMonthDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle5;
            threeMonthDataGridViewTextBoxColumn.HeaderText = "3 Mo";
            threeMonthDataGridViewTextBoxColumn.Name = "threeMonthDataGridViewTextBoxColumn";
            threeMonthDataGridViewTextBoxColumn.ReadOnly = true;
            threeMonthDataGridViewTextBoxColumn.Width = 60;
            // 
            // sixMonthDataGridViewTextBoxColumn
            // 
            sixMonthDataGridViewTextBoxColumn.DataPropertyName = "SixMonth";
            dataGridViewCellStyle6.Format = "#.00";
            sixMonthDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle6;
            sixMonthDataGridViewTextBoxColumn.HeaderText = "6 Mo";
            sixMonthDataGridViewTextBoxColumn.Name = "sixMonthDataGridViewTextBoxColumn";
            sixMonthDataGridViewTextBoxColumn.ReadOnly = true;
            sixMonthDataGridViewTextBoxColumn.Width = 60;
            // 
            // oneYearDataGridViewTextBoxColumn
            // 
            oneYearDataGridViewTextBoxColumn.DataPropertyName = "OneYear";
            dataGridViewCellStyle7.Format = "#.00";
            oneYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle7;
            oneYearDataGridViewTextBoxColumn.HeaderText = "1 Yr";
            oneYearDataGridViewTextBoxColumn.Name = "oneYearDataGridViewTextBoxColumn";
            oneYearDataGridViewTextBoxColumn.ReadOnly = true;
            oneYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // twoYearDataGridViewTextBoxColumn
            // 
            twoYearDataGridViewTextBoxColumn.DataPropertyName = "TwoYear";
            dataGridViewCellStyle8.Format = "#.00";
            twoYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle8;
            twoYearDataGridViewTextBoxColumn.HeaderText = "2 Yr";
            twoYearDataGridViewTextBoxColumn.Name = "twoYearDataGridViewTextBoxColumn";
            twoYearDataGridViewTextBoxColumn.ReadOnly = true;
            twoYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // threeYearDataGridViewTextBoxColumn
            // 
            threeYearDataGridViewTextBoxColumn.DataPropertyName = "ThreeYear";
            dataGridViewCellStyle9.Format = "#.00";
            threeYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle9;
            threeYearDataGridViewTextBoxColumn.HeaderText = "3 Yr";
            threeYearDataGridViewTextBoxColumn.Name = "threeYearDataGridViewTextBoxColumn";
            threeYearDataGridViewTextBoxColumn.ReadOnly = true;
            threeYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // fiveYearDataGridViewTextBoxColumn
            // 
            fiveYearDataGridViewTextBoxColumn.DataPropertyName = "FiveYear";
            dataGridViewCellStyle10.Format = "#.00";
            fiveYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle10;
            fiveYearDataGridViewTextBoxColumn.HeaderText = "5 Yr";
            fiveYearDataGridViewTextBoxColumn.Name = "fiveYearDataGridViewTextBoxColumn";
            fiveYearDataGridViewTextBoxColumn.ReadOnly = true;
            fiveYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // sevenYearDataGridViewTextBoxColumn
            // 
            sevenYearDataGridViewTextBoxColumn.DataPropertyName = "SevenYear";
            dataGridViewCellStyle11.Format = "#.00";
            sevenYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle11;
            sevenYearDataGridViewTextBoxColumn.HeaderText = "7 Yr";
            sevenYearDataGridViewTextBoxColumn.Name = "sevenYearDataGridViewTextBoxColumn";
            sevenYearDataGridViewTextBoxColumn.ReadOnly = true;
            sevenYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // tenYearDataGridViewTextBoxColumn
            // 
            tenYearDataGridViewTextBoxColumn.DataPropertyName = "TenYear";
            dataGridViewCellStyle12.Format = "#.00";
            tenYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle12;
            tenYearDataGridViewTextBoxColumn.HeaderText = "10 Yr";
            tenYearDataGridViewTextBoxColumn.Name = "tenYearDataGridViewTextBoxColumn";
            tenYearDataGridViewTextBoxColumn.ReadOnly = true;
            tenYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // twentyYearDataGridViewTextBoxColumn
            // 
            twentyYearDataGridViewTextBoxColumn.DataPropertyName = "TwentyYear";
            dataGridViewCellStyle13.Format = "#.00";
            twentyYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle13;
            twentyYearDataGridViewTextBoxColumn.HeaderText = "20 Yr";
            twentyYearDataGridViewTextBoxColumn.Name = "twentyYearDataGridViewTextBoxColumn";
            twentyYearDataGridViewTextBoxColumn.ReadOnly = true;
            twentyYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // thirtyYearDataGridViewTextBoxColumn
            // 
            thirtyYearDataGridViewTextBoxColumn.DataPropertyName = "ThirtyYear";
            dataGridViewCellStyle14.Format = "#.00";
            thirtyYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle14;
            thirtyYearDataGridViewTextBoxColumn.HeaderText = "30 Yr";
            thirtyYearDataGridViewTextBoxColumn.Name = "thirtyYearDataGridViewTextBoxColumn";
            thirtyYearDataGridViewTextBoxColumn.ReadOnly = true;
            thirtyYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // YieldCurveRateEditorControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(gridYieldCurveRates);
            Controls.Add(pnlTimePeriod);
            DoubleBuffered = true;
            Margin = new Padding(2);
            Name = "YieldCurveRateEditorControl";
            Size = new Size(1100, 468);
            pnlTimePeriod.ResumeLayout(false);
            pnlTimePeriod.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)yieldCurveRatesBindingSource).EndInit();
            ((System.ComponentModel.ISupportInitialize)gridYieldCurveRates).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlTimePeriod;
        private System.Windows.Forms.ComboBox ddlTimePeriod;
        private System.Windows.Forms.Label lblTimePeriod;
        private System.Windows.Forms.BindingSource yieldCurveRatesBindingSource;
        private System.Windows.Forms.DataGridView gridYieldCurveRates;
        private System.Windows.Forms.DataGridViewTextBoxColumn valueDateDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn oneMonthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn TwoMonth;
        private System.Windows.Forms.DataGridViewTextBoxColumn threeMonthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn sixMonthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn oneYearDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn twoYearDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn threeYearDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn fiveYearDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn sevenYearDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn tenYearDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn twentyYearDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn thirtyYearDataGridViewTextBoxColumn;
    }
}
