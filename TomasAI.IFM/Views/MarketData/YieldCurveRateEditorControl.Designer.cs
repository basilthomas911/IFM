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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle15 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle16 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle13 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle14 = new System.Windows.Forms.DataGridViewCellStyle();
            this.pnlTimePeriod = new System.Windows.Forms.Panel();
            this.ddlTimePeriod = new System.Windows.Forms.ComboBox();
            this.lblTimePeriod = new System.Windows.Forms.Label();
            this.yieldCurveRatesBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.gridYieldCurveRates = new System.Windows.Forms.DataGridView();
            this.valueDateDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.oneMonthDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.TwoMonth = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.threeMonthDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.sixMonthDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.oneYearDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.twoYearDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.threeYearDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.fiveYearDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.sevenYearDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tenYearDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.twentyYearDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.thirtyYearDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.pnlTimePeriod.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.yieldCurveRatesBindingSource)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridYieldCurveRates)).BeginInit();
            this.SuspendLayout();
            // 
            // pnlTimePeriod
            // 
            this.pnlTimePeriod.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlTimePeriod.Controls.Add(this.ddlTimePeriod);
            this.pnlTimePeriod.Controls.Add(this.lblTimePeriod);
            this.pnlTimePeriod.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTimePeriod.Location = new System.Drawing.Point(0, 0);
            this.pnlTimePeriod.Margin = new System.Windows.Forms.Padding(2);
            this.pnlTimePeriod.Name = "pnlTimePeriod";
            this.pnlTimePeriod.Size = new System.Drawing.Size(943, 28);
            this.pnlTimePeriod.TabIndex = 0;
            // 
            // ddlTimePeriod
            // 
            this.ddlTimePeriod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlTimePeriod.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ddlTimePeriod.FormattingEnabled = true;
            this.ddlTimePeriod.Location = new System.Drawing.Point(104, 2);
            this.ddlTimePeriod.Margin = new System.Windows.Forms.Padding(2);
            this.ddlTimePeriod.Name = "ddlTimePeriod";
            this.ddlTimePeriod.Size = new System.Drawing.Size(213, 24);
            this.ddlTimePeriod.TabIndex = 1;
            this.ddlTimePeriod.SelectedIndexChanged += new System.EventHandler(this.ddlTimePeriod_SelectedIndexChanged);
            // 
            // lblTimePeriod
            // 
            this.lblTimePeriod.AutoSize = true;
            this.lblTimePeriod.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTimePeriod.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblTimePeriod.Location = new System.Drawing.Point(12, 5);
            this.lblTimePeriod.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblTimePeriod.Name = "lblTimePeriod";
            this.lblTimePeriod.Size = new System.Drawing.Size(88, 17);
            this.lblTimePeriod.TabIndex = 0;
            this.lblTimePeriod.Text = "Time Period:";
            this.lblTimePeriod.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // yieldCurveRatesBindingSource
            // 
            this.yieldCurveRatesBindingSource.DataSource = typeof(TomasAI.IFM.Shared.MarketData.ViewModels.YieldCurveRateReadModel);
            // 
            // gridYieldCurveRates
            // 
            this.gridYieldCurveRates.AllowUserToAddRows = false;
            this.gridYieldCurveRates.AllowUserToDeleteRows = false;
            this.gridYieldCurveRates.AllowUserToResizeRows = false;
            this.gridYieldCurveRates.AutoGenerateColumns = false;
            this.gridYieldCurveRates.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.DisplayedCells;
            this.gridYieldCurveRates.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridYieldCurveRates.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridYieldCurveRates.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridYieldCurveRates.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.valueDateDataGridViewTextBoxColumn,
            this.oneMonthDataGridViewTextBoxColumn,
            this.TwoMonth,
            this.threeMonthDataGridViewTextBoxColumn,
            this.sixMonthDataGridViewTextBoxColumn,
            this.oneYearDataGridViewTextBoxColumn,
            this.twoYearDataGridViewTextBoxColumn,
            this.threeYearDataGridViewTextBoxColumn,
            this.fiveYearDataGridViewTextBoxColumn,
            this.sevenYearDataGridViewTextBoxColumn,
            this.tenYearDataGridViewTextBoxColumn,
            this.twentyYearDataGridViewTextBoxColumn,
            this.thirtyYearDataGridViewTextBoxColumn});
            this.gridYieldCurveRates.DataSource = this.yieldCurveRatesBindingSource;
            dataGridViewCellStyle15.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle15.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            dataGridViewCellStyle15.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle15.ForeColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle15.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle15.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle15.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridYieldCurveRates.DefaultCellStyle = dataGridViewCellStyle15;
            this.gridYieldCurveRates.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridYieldCurveRates.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.gridYieldCurveRates.Location = new System.Drawing.Point(0, 28);
            this.gridYieldCurveRates.Margin = new System.Windows.Forms.Padding(2);
            this.gridYieldCurveRates.MultiSelect = false;
            this.gridYieldCurveRates.Name = "gridYieldCurveRates";
            this.gridYieldCurveRates.ReadOnly = true;
            dataGridViewCellStyle16.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle16.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle16.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle16.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle16.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle16.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle16.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridYieldCurveRates.RowHeadersDefaultCellStyle = dataGridViewCellStyle16;
            this.gridYieldCurveRates.RowTemplate.Height = 28;
            this.gridYieldCurveRates.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.gridYieldCurveRates.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridYieldCurveRates.Size = new System.Drawing.Size(943, 378);
            this.gridYieldCurveRates.TabIndex = 3;
            // 
            // valueDateDataGridViewTextBoxColumn
            // 
            this.valueDateDataGridViewTextBoxColumn.DataPropertyName = "ValueDate";
            dataGridViewCellStyle2.Format = "yyyy-MM-dd";
            this.valueDateDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle2;
            this.valueDateDataGridViewTextBoxColumn.HeaderText = "Date";
            this.valueDateDataGridViewTextBoxColumn.Name = "valueDateDataGridViewTextBoxColumn";
            this.valueDateDataGridViewTextBoxColumn.ReadOnly = true;
            this.valueDateDataGridViewTextBoxColumn.Width = 180;
            // 
            // oneMonthDataGridViewTextBoxColumn
            // 
            this.oneMonthDataGridViewTextBoxColumn.DataPropertyName = "OneMonth";
            dataGridViewCellStyle3.Format = "N2";
            dataGridViewCellStyle3.NullValue = null;
            this.oneMonthDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle3;
            this.oneMonthDataGridViewTextBoxColumn.HeaderText = "1 Mo";
            this.oneMonthDataGridViewTextBoxColumn.Name = "oneMonthDataGridViewTextBoxColumn";
            this.oneMonthDataGridViewTextBoxColumn.ReadOnly = true;
            this.oneMonthDataGridViewTextBoxColumn.Width = 60;
            // 
            // TwoMonth
            // 
            this.TwoMonth.DataPropertyName = "TwoMonth";
            dataGridViewCellStyle4.Format = "N2";
            this.TwoMonth.DefaultCellStyle = dataGridViewCellStyle4;
            this.TwoMonth.HeaderText = "2 Mo";
            this.TwoMonth.Name = "TwoMonth";
            this.TwoMonth.ReadOnly = true;
            this.TwoMonth.Width = 60;
            // 
            // threeMonthDataGridViewTextBoxColumn
            // 
            this.threeMonthDataGridViewTextBoxColumn.DataPropertyName = "ThreeMonth";
            dataGridViewCellStyle5.Format = "N2";
            this.threeMonthDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle5;
            this.threeMonthDataGridViewTextBoxColumn.HeaderText = "3 Mo";
            this.threeMonthDataGridViewTextBoxColumn.Name = "threeMonthDataGridViewTextBoxColumn";
            this.threeMonthDataGridViewTextBoxColumn.ReadOnly = true;
            this.threeMonthDataGridViewTextBoxColumn.Width = 60;
            // 
            // sixMonthDataGridViewTextBoxColumn
            // 
            this.sixMonthDataGridViewTextBoxColumn.DataPropertyName = "SixMonth";
            dataGridViewCellStyle6.Format = "#.00";
            this.sixMonthDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle6;
            this.sixMonthDataGridViewTextBoxColumn.HeaderText = "6 Mo";
            this.sixMonthDataGridViewTextBoxColumn.Name = "sixMonthDataGridViewTextBoxColumn";
            this.sixMonthDataGridViewTextBoxColumn.ReadOnly = true;
            this.sixMonthDataGridViewTextBoxColumn.Width = 60;
            // 
            // oneYearDataGridViewTextBoxColumn
            // 
            this.oneYearDataGridViewTextBoxColumn.DataPropertyName = "OneYear";
            dataGridViewCellStyle7.Format = "#.00";
            this.oneYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle7;
            this.oneYearDataGridViewTextBoxColumn.HeaderText = "1 Yr";
            this.oneYearDataGridViewTextBoxColumn.Name = "oneYearDataGridViewTextBoxColumn";
            this.oneYearDataGridViewTextBoxColumn.ReadOnly = true;
            this.oneYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // twoYearDataGridViewTextBoxColumn
            // 
            this.twoYearDataGridViewTextBoxColumn.DataPropertyName = "TwoYear";
            dataGridViewCellStyle8.Format = "#.00";
            this.twoYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle8;
            this.twoYearDataGridViewTextBoxColumn.HeaderText = "2 Yr";
            this.twoYearDataGridViewTextBoxColumn.Name = "twoYearDataGridViewTextBoxColumn";
            this.twoYearDataGridViewTextBoxColumn.ReadOnly = true;
            this.twoYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // threeYearDataGridViewTextBoxColumn
            // 
            this.threeYearDataGridViewTextBoxColumn.DataPropertyName = "ThreeYear";
            dataGridViewCellStyle9.Format = "#.00";
            this.threeYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle9;
            this.threeYearDataGridViewTextBoxColumn.HeaderText = "3 Yr";
            this.threeYearDataGridViewTextBoxColumn.Name = "threeYearDataGridViewTextBoxColumn";
            this.threeYearDataGridViewTextBoxColumn.ReadOnly = true;
            this.threeYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // fiveYearDataGridViewTextBoxColumn
            // 
            this.fiveYearDataGridViewTextBoxColumn.DataPropertyName = "FiveYear";
            dataGridViewCellStyle10.Format = "#.00";
            this.fiveYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle10;
            this.fiveYearDataGridViewTextBoxColumn.HeaderText = "5 Yr";
            this.fiveYearDataGridViewTextBoxColumn.Name = "fiveYearDataGridViewTextBoxColumn";
            this.fiveYearDataGridViewTextBoxColumn.ReadOnly = true;
            this.fiveYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // sevenYearDataGridViewTextBoxColumn
            // 
            this.sevenYearDataGridViewTextBoxColumn.DataPropertyName = "SevenYear";
            dataGridViewCellStyle11.Format = "#.00";
            this.sevenYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle11;
            this.sevenYearDataGridViewTextBoxColumn.HeaderText = "7 Yr";
            this.sevenYearDataGridViewTextBoxColumn.Name = "sevenYearDataGridViewTextBoxColumn";
            this.sevenYearDataGridViewTextBoxColumn.ReadOnly = true;
            this.sevenYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // tenYearDataGridViewTextBoxColumn
            // 
            this.tenYearDataGridViewTextBoxColumn.DataPropertyName = "TenYear";
            dataGridViewCellStyle12.Format = "#.00";
            this.tenYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle12;
            this.tenYearDataGridViewTextBoxColumn.HeaderText = "10 Yr";
            this.tenYearDataGridViewTextBoxColumn.Name = "tenYearDataGridViewTextBoxColumn";
            this.tenYearDataGridViewTextBoxColumn.ReadOnly = true;
            this.tenYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // twentyYearDataGridViewTextBoxColumn
            // 
            this.twentyYearDataGridViewTextBoxColumn.DataPropertyName = "TwentyYear";
            dataGridViewCellStyle13.Format = "#.00";
            this.twentyYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle13;
            this.twentyYearDataGridViewTextBoxColumn.HeaderText = "20 Yr";
            this.twentyYearDataGridViewTextBoxColumn.Name = "twentyYearDataGridViewTextBoxColumn";
            this.twentyYearDataGridViewTextBoxColumn.ReadOnly = true;
            this.twentyYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // thirtyYearDataGridViewTextBoxColumn
            // 
            this.thirtyYearDataGridViewTextBoxColumn.DataPropertyName = "ThirtyYear";
            dataGridViewCellStyle14.Format = "#.00";
            this.thirtyYearDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle14;
            this.thirtyYearDataGridViewTextBoxColumn.HeaderText = "30 Yr";
            this.thirtyYearDataGridViewTextBoxColumn.Name = "thirtyYearDataGridViewTextBoxColumn";
            this.thirtyYearDataGridViewTextBoxColumn.ReadOnly = true;
            this.thirtyYearDataGridViewTextBoxColumn.Width = 60;
            // 
            // YieldCurveRateEditorControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.gridYieldCurveRates);
            this.Controls.Add(this.pnlTimePeriod);
            this.DoubleBuffered = true;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "YieldCurveRateEditorControl";
            this.Size = new System.Drawing.Size(943, 406);
            this.pnlTimePeriod.ResumeLayout(false);
            this.pnlTimePeriod.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.yieldCurveRatesBindingSource)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridYieldCurveRates)).EndInit();
            this.ResumeLayout(false);

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
