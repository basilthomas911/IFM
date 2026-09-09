using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Fund posting editor with persisted recovery. Operators select configured rules and existing transactions.</summary>
public sealed class FundFinancialPostingForm:DarkTradingForm
{
    readonly IPortfolioFinancialApi _api;
    readonly FinancialReadScope _scope;
    readonly FinancialPostingOperation _operations;
    readonly FinancialTransactionRow? _related;
    readonly ComboBox _kind=PortfolioUiStyle.Combo("Financial transaction type");
    readonly NumericUpDown _amount=new() { DecimalPlaces=2,Minimum=.01m,Maximum=1000000000,ThousandsSeparator=true,Dock=DockStyle.Fill,AccessibleName="Transaction amount" };
    readonly DateTimePicker _date=new Trade.IronCondor.DarkDateTimePicker() { Format=DateTimePickerFormat.Short,Dock=DockStyle.Fill,AccessibleName="Accounting date" };
    readonly DateTimePicker _valueDate=new Trade.IronCondor.DarkDateTimePicker() { Format=DateTimePickerFormat.Short,Dock=DockStyle.Fill,AccessibleName="Value date" };
    readonly TextBox _description=PortfolioUiStyle.TextBox("Transaction description");
    readonly TextBox _reference=PortfolioUiStyle.TextBox("Cash movement reference");
    readonly CheckBox _confirmed=new() { Text="I confirm this cash movement",AutoSize=true,AccessibleName="Confirm cash movement" };
    readonly Label _status=new() { Dock=DockStyle.Fill,AutoEllipsis=true,AccessibleName="Financial posting status" };
    readonly Button _submit=PortfolioUiStyle.Button("Post","Post financial transaction");
    readonly CancellationTokenSource _lifetime=new();
    FinancialRead<FinancialPostingConfiguration>? _configuration;
    LedgerPostingReceipt? _relatedReceipt;
    PendingFinancialOperation? _pending;
    bool _busy;

    public FundFinancialPostingForm(IPortfolioFinancialApi api,FinancialReadScope scope,IPendingFinancialOperationStore pendingStore,
        FinancialTransactionRow? related=null,PendingFinancialOperation? pending=null)
    {
        _api=api;_scope=scope;_related=related;_pending=pending;_operations=new(api,pendingStore);
        _submit.Enabled=false;_status.Text="Loading posting configuration...";
        Text="Fund transaction";Name="FundFinancialPostingForm";Size=new(690,530);MinimumSize=new(650,500);PortfolioUiStyle.Apply(this);
        var fields=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new(12),ColumnCount=2,RowCount=10 };
        fields.ColumnStyles.Add(new(SizeType.Absolute,170));fields.ColumnStyles.Add(new(SizeType.Percent,100));
        for(var i=0;i<8;i++) fields.RowStyles.Add(new(SizeType.Absolute,i==4?64:38));
        fields.RowStyles.Add(new(SizeType.Percent,100));fields.RowStyles.Add(new(SizeType.Absolute,48));
        Add("Transaction",_kind,0);Add("Amount (USD)",_amount,1);Add("Accounting date",_date,2);Add("Value date",_valueDate,3);
        _description.Multiline=true;_description.MaxLength=1000;Add("Description",_description,4);
        _reference.MaxLength=256;Add("Movement reference",_reference,5);fields.Controls.Add(_confirmed,1,6);
        var originalText=related is not null ? $"Transaction {related.TransactionId}; journal {related.JournalId}"
            : pending?.Request.Body.RelatedJournalId is { } journal ? $"Journal {journal}"
            : pending?.Request.Body.RelatedObligationId is { } obligation ? $"Withdrawal {obligation}" : "None selected";
        var original=new TextBox { ReadOnly=true,Dock=DockStyle.Fill,Text=originalText,AccessibleName="Original transaction" };
        Add("Original transaction",original,7);fields.Controls.Add(_status,0,8);fields.SetColumnSpan(_status,2);
        var close=PortfolioUiStyle.Button("Close","Close financial posting");close.Click+=(_,_)=>Close();CancelButton=close;
        var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false };
        buttons.Controls.AddRange([close,_submit]);fields.Controls.Add(buttons,0,9);fields.SetColumnSpan(buttons,2);
        Controls.Add(fields);
        _kind.SelectedIndexChanged+=(_,_)=>
        {
            if(_pending is not null) return;
            var opening=(_kind.SelectedItem as RuleChoice)?.Rule.Kind==LedgerTransactionKind.OpeningBalance;
            _confirmed.Enabled=_reference.Enabled=!opening;
            _confirmed.Text=opening?"Development capital; no external movement":"I confirm this cash movement";
            if(opening) { _confirmed.Checked=false;_reference.Clear(); }
        };
        _date.ValueChanged+=async(_,_)=> { if(_pending is null) await LoadConfigurationAsync(); };
        _submit.Click+=async(_,_)=>await SubmitAsync();
        Shown+=async(_,_)=> { if(_pending is null) await LoadConfigurationAsync(); else BindPending(); };
        FormClosed+=(_,_)=> { _lifetime.Cancel();_lifetime.Dispose(); };
        if(related is not null) _amount.Value=Math.Clamp(Math.Abs(related.Transaction.Amount),_amount.Minimum,_amount.Maximum);
        if(pending is not null)
        { _kind.DataSource=new[] { $"{pending.Request.Body.TransactionKind} (version {pending.Request.Body.PostingRule.Version})" };
            _amount.Value=Math.Clamp(pending.Request.Body.Amount,_amount.Minimum,_amount.Maximum);_date.Value=pending.Request.Body.AccountingDate.ToDateTime(TimeOnly.MinValue);
            _valueDate.Value=pending.Request.Body.ValueDate.ToDateTime(TimeOnly.MinValue);_description.Text=pending.Request.Body.Description;
            _reference.Text=pending.Request.Body.MovementEvidence.SourceReference;_confirmed.Checked=pending.Request.Body.MovementEvidence.Status==MovementStatus.Confirmed; }
        void Add(string text,Control control,int row)
        {
            var label=PortfolioUiStyle.Caption(text);label.AutoSize=false;label.Padding=new(0,0,8,0);label.TextAlign=ContentAlignment.MiddleRight;
            fields.Controls.Add(label,0,row);fields.Controls.Add(control,1,row);
        }
    }
    async Task LoadConfigurationAsync()
    {
        if(_busy || IsDisposed) return;_busy=true;_submit.Enabled=false;_date.Enabled=false;
        try
        {
            var selectedDate=DateOnly.FromDateTime(_date.Value);
            var result=await _api.GetFinancialPostingConfigurationAsync(_scope,new(selectedDate),_lifetime.Token);
            if(IsDisposed) return;
            if(!result.Success || result.Value?.Value is null) throw new InvalidOperationException(result.ErrorMessage??"Posting configuration unavailable.");
            if(selectedDate!=DateOnly.FromDateTime(_date.Value)) { _configuration=null;return; }
            _configuration=result.Value;
            if(_related is not null)
            {
                var receipt=await _api.GetPostingReceiptAsync(_scope,new(_related.OperationId),_lifetime.Token);
                _relatedReceipt=receipt.Value?.Value?.Posting?.Receipt;
            }
            if(IsDisposed) return;
            var rules=_configuration.Value.Rules.Where(x=>x.Kind is LedgerTransactionKind.DepositConfirmed or LedgerTransactionKind.WithdrawalRequested ||
                (x.Kind==LedgerTransactionKind.OpeningBalance && _configuration.Value.AllowDevelopmentOpeningCapital &&
                    (_scope.Access.Roles.Contains("PortfolioAdministrator") || _scope.Access.Roles.Contains("LedgerImport"))) ||
                (_related?.JournalId is >0 && x.Kind==LedgerTransactionKind.Reversal) ||
                (_related?.Transaction.TransactionKind==LedgerTransactionKind.WithdrawalRequested && x.Kind is LedgerTransactionKind.WithdrawalSettled or LedgerTransactionKind.WithdrawalCancelled)).ToArray();
            _kind.DisplayMember=nameof(RuleChoice.Label);_kind.DataSource=rules.Select(x=>new RuleChoice(x)).ToArray();
            _status.Text=_configuration.Value.PeriodOpen?"Choose a configured transaction. Generated identities are assigned when you post.":"This accounting period is closed.";
            _submit.Enabled=_configuration.Value.PeriodOpen && rules.Length>0;
        }
        catch(Exception error) { if(!IsDisposed) _status.Text=error.Message; }
        finally { _busy=false;if(!IsDisposed) _date.Enabled=_pending is null; }
    }
    async Task SubmitAsync()
    {
        if(_busy || IsDisposed) return;_busy=true;_submit.Enabled=false;string? errorMessage=null;
        try
        {
            if(_pending is null)
            {
                if(_configuration is null || _kind.SelectedItem is not RuleChoice choice) throw new InvalidOperationException("Select a configured posting rule.");
                var draft=new FinancialPostingDraft(choice.Rule.Kind,_amount.Value,DateOnly.FromDateTime(_date.Value),DateOnly.FromDateTime(_valueDate.Value),
                    _description.Text,_reference.Text,_confirmed.Checked);
                var request=FinancialPostingPreparation.Create(_scope,_configuration,choice.Rule,draft,_related,_relatedReceipt,DateTime.UtcNow);
                _pending=new(request,PendingFinancialPhase.Prepared,"Prepared; retaining this operation identity.");
                _pending=await _operations.SubmitAsync(request,_lifetime.Token);
            }
            else _pending=await _operations.RecoverAsync(_pending.Request.OperationId,_lifetime.Token);
        }
        catch(Exception error) { errorMessage=error.Message; }
        finally { _busy=false;if(!IsDisposed) { BindPending();if(errorMessage is not null) _status.Text=errorMessage; } }
    }
    void BindPending()
    {
        if(_pending is null) { _submit.Enabled=_configuration?.Value?.PeriodOpen==true;return; }
        foreach(var field in new Control[] { _kind,_amount,_date,_valueDate,_description,_reference,_confirmed }) field.Enabled=false;
        _status.Text=$"{_pending.Message}\r\nOperation: {_pending.Request.OperationId}";
        _submit.Text="Check outcome";
        _submit.Enabled=_pending.Phase is PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown;
    }
    sealed record RuleChoice(LedgerPostingRule Rule)
    {
        public string Label=>Rule.Kind switch
        {
            LedgerTransactionKind.DepositConfirmed=>"Confirmed deposit",
            LedgerTransactionKind.OpeningBalance=>"Development opening capital",
            LedgerTransactionKind.WithdrawalRequested=>"Request withdrawal",
            LedgerTransactionKind.WithdrawalSettled=>"Confirm selected withdrawal",
            LedgerTransactionKind.WithdrawalCancelled=>"Cancel selected withdrawal",
            LedgerTransactionKind.Reversal=>"Reverse selected journal",
            _=>Rule.Kind.ToString()
        }+$" (version {Rule.Version})";
    }
}
