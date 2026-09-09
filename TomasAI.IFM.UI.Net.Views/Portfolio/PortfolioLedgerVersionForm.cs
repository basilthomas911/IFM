using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Prepares immutable account/rule versions; the parent persists and recovers the resulting command.</summary>
public sealed class PortfolioLedgerVersionForm : DarkTradingForm
{
    readonly ComboBox _normal=PortfolioUiStyle.Combo("Account normal side");
    readonly CheckBox _dimension=new() { Text="Require Fund dimension",AutoSize=true };
    readonly CheckBox _confirmed=new() { Text="Require confirmed movement",AutoSize=true };
    readonly ComboBox _debit=PortfolioUiStyle.Combo("Debit account version");
    readonly ComboBox _credit=PortfolioUiStyle.Combo("Credit account version");
    readonly ComboBox _asset=PortfolioUiStyle.Combo("Valuation asset account version");
    readonly ComboBox _pnl=PortfolioUiStyle.Combo("Unrealized P&L account version");
    readonly DateTimePicker _effective=new Trade.IronCondor.DarkDateTimePicker { Format=DateTimePickerFormat.Short };
    readonly TextBox _reason=PortfolioUiStyle.TextBox("Configuration version audit reason");
    readonly Label _error=new() { Dock=DockStyle.Fill,AutoEllipsis=true };
    public ConfigureLedgerCommand? PreparedCommand { get; private set; }

    public PortfolioLedgerVersionForm(FinancialReadScope scope,FinancialRead<FinancialLedgerConfiguration> snapshot,
        FinancialConfiguredAccount? account=null,FinancialConfiguredRule? rule=null)
    {
        if((account is null)==(rule is null)) throw new ArgumentException("Select one configured account or rule.");
        Text=account is not null?"Edit ledger account":"Edit posting rule";Name="PortfolioLedgerVersionForm";
        Size=new(670,550);MinimumSize=new(610,550);PortfolioUiStyle.Apply(this);
        var fields=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new(12),ColumnCount=2,RowCount=11 };
        fields.ColumnStyles.Add(new(SizeType.Absolute,150));fields.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<9;i++) fields.RowStyles.Add(new(SizeType.Absolute,39));
        fields.RowStyles.Add(new(SizeType.Percent,100));fields.RowStyles.Add(new(SizeType.Absolute,46));
        void Add(string label,Control value,int row) { fields.Controls.Add(new Label { Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },0,row);value.Dock=DockStyle.Fill;fields.Controls.Add(value,1,row); }
        Add("Version",new Label { Text=account is not null?$"{account.Definition.Category}: {account.Definition.Version} → {account.Definition.Version+1}":$"{rule!.Definition.Kind}: {rule.Definition.Version} → {rule.Definition.Version+1}",TextAlign=ContentAlignment.MiddleLeft },0);
        _normal.DataSource=new[] { PostingSide.Debit,PostingSide.Credit };
        Add("Normal side",_normal,1);Add("Scope",_dimension,2);Add("Debit",_debit,3);Add("Credit",_credit,4);
        Add("Valuation asset",_asset,5);Add("Unrealized P&L",_pnl,6);Add("Movement",_confirmed,7);Add("Effective from",_effective,8);
        _reason.Multiline=true;_reason.MaxLength=1024;Add("Audit reason",_reason,9);
        var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false };
        var save=PortfolioUiStyle.Button("Review and save","Prepare configuration version");var cancel=PortfolioUiStyle.Button("Cancel","Cancel configuration edit");
        cancel.DialogResult=DialogResult.Cancel;CancelButton=cancel;actions.Controls.AddRange([save,cancel]);fields.Controls.Add(actions,1,10);
        var root=new Panel { Dock=DockStyle.Fill };_error.Dock=DockStyle.Bottom;_error.Height=38;root.Controls.Add(fields);root.Controls.Add(_error);Controls.Add(root);
        var choices=snapshot.Value!.Accounts.Where(x=>x.State=="Active").Select(x=>new AccountChoice(new(x.Definition.AccountId,x.Definition.Version),$"{x.Definition.Category} / {x.Definition.AccountId} v{x.Definition.Version}")).ToArray();
        foreach(var combo in new[] { _debit,_credit,_asset,_pnl })
        { combo.DisplayMember=nameof(AccountChoice.Label);combo.DataSource=(combo==_asset || combo==_pnl?new[] { new AccountChoice(null,"None") }.Concat(choices):choices).ToArray(); }
        _normal.Enabled=_dimension.Enabled=account is not null;
        foreach(var control in new Control[] { _debit,_credit,_asset,_pnl,_confirmed,_effective }) control.Enabled=rule is not null;
        foreach(var row in account is not null?new[] { 3,4,5,6,7,8 }:new[] { 1,2 })
        {
            foreach(var control in fields.Controls.Cast<Control>().Where(x=>fields.GetRow(x)==row)) control.Visible=false;
            fields.RowStyles[row].Height=0;
        }
        MinimumSize=new(610,account is not null?340:480);Size=new(670,MinimumSize.Height);
        if(account is not null) { _normal.SelectedItem=account.Definition.NormalSide;_dimension.Checked=account.Definition.FundDimensionRequired; }
        if(rule is not null)
        {
            void Select(ComboBox combo,LedgerAccountBinding? binding) { combo.SelectedItem=((AccountChoice[])combo.DataSource!).FirstOrDefault(x=>x.Binding==binding); }
            Select(_debit,rule.Definition.Debit);Select(_credit,rule.Definition.Credit);Select(_asset,rule.Definition.ValuationAsset);Select(_pnl,rule.Definition.UnrealizedPnl);
            _confirmed.Checked=rule.Definition.RequiresConfirmedMovement;
        }
        save.Click+=(_,_)=>
        {
            try
            {
                LedgerAccountBinding? Binding(ComboBox combo)=>(combo.SelectedItem as AccountChoice)?.Binding;
                PreparedCommand=account is not null
                    ?FinancialControlPreparation.EditAccount(scope,snapshot,account.Definition.AccountId,(PostingSide)_normal.SelectedItem!,_dimension.Checked,_reason.Text,DateTime.UtcNow)
                    :FinancialControlPreparation.EditRule(scope,snapshot,rule!.Definition.RuleId,Binding(_debit)??throw new ArgumentException("Select a debit account."),
                        Binding(_credit)??throw new ArgumentException("Select a credit account."),_confirmed.Checked,Binding(_asset),Binding(_pnl),DateOnly.FromDateTime(_effective.Value),_reason.Text,DateTime.UtcNow);
                DialogResult=DialogResult.OK;Close();
            }
            catch(Exception error) { _error.Text=error.Message; }
        };
    }
    sealed record AccountChoice(LedgerAccountBinding? Binding,string Label);
}
