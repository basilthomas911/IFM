namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Exposes the quote-selection click to keyboard and UI Automation clients.</summary>
public sealed class MarketSelectionDataGridView : DataGridView
{
    protected override void OnColumnAdded(DataGridViewColumnEventArgs e)
    {
        if (Name == "marketSelectionGrid" && e.Column.CellTemplate is DataGridViewTextBoxCell)
            e.Column.CellTemplate = new SelectionCell();
        base.OnColumnAdded(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Name == "marketSelectionGrid" && CurrentCell is { } cell && e.KeyCode is Keys.Space or Keys.Enter)
        {
            Activate(cell);
            e.Handled = e.SuppressKeyPress = true;
            return;
        }
        base.OnKeyDown(e);
    }

    void Activate(DataGridViewCell cell)
    {
        if (InvokeRequired) { BeginInvoke((Action)(() => Activate(cell))); return; }
        if (!Enabled || cell.RowIndex < 0) return;
        CurrentCell = cell;
        OnCellClick(new(cell.ColumnIndex, cell.RowIndex));
    }

    sealed class SelectionCell : DataGridViewTextBoxCell
    {
        public SelectionCell() { }
        protected override AccessibleObject CreateAccessibilityInstance() => new SelectionAccessibleObject(this);

        sealed class SelectionAccessibleObject : DataGridViewCellAccessibleObject
        {
            readonly DataGridViewCell _cell;
            public SelectionAccessibleObject(DataGridViewCell owner) : base(owner) { _cell = owner; }
            public override string DefaultAction => "Select option leg";
            public override void DoDefaultAction()
            {
                if (_cell.DataGridView is MarketSelectionDataGridView grid) grid.Activate(_cell);
            }
        }
    }
}
