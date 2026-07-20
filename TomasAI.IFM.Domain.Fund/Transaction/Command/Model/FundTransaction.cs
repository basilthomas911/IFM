using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Model;

/// <summary>
/// fund transaction
/// </summary>
public class FundTransaction : IDataValidation, IFundTransaction
{
     readonly IValidator<FundTransaction> _validator;

    /// <summary>
    /// fund transaction constructor
    /// </summary>
    /// <param name="transactionId"></param>
    /// <param name="transactionDate"></param>
    /// <param name="transactionType"></param>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <param name="tradeStatus"></param>
    /// <param name="description"></param>
    /// <param name="amount"></param>
    /// <param name="balance"></param>
    public FundTransaction(
        FundTransactionId transactionId,
        DateTime transactionDate,
        FundTransactionType transactionType,
        int fundId,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        string description,
        decimal amount,
        decimal balance)
    {
        TransactionId = transactionId;
        TransactionDate = transactionDate;
        TransactionType = transactionType;
        FundId = fundId;
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        TradeStatus = tradeStatus;
        Description = description;
        Amount = amount;
        Balance = balance;
        _validator ??= new FundTransactionValidator();
        this.Validate(_validator);
    }

    public FundTransactionId TransactionId { get; }
    public DateTime TransactionDate { get; }
    public FundTransactionType TransactionType { get; }
    public int FundId { get; }
    public int OrderId { get; }
    public int TradeId { get; }
    public TradeType TradeType { get; }
    public DateOnly ValueDate { get; }
    public TradeStatus TradeStatus { get; }
    public string Description { get; }
    public decimal Amount { get; private set; }
    public decimal Balance { get; private set; }

    /// <summary>
    /// create fund transaction from view model
    /// </summary>
    /// <param name="e"></param>
    public FundTransaction(FundTransactionReadModel e) : this(
        transactionId: e.Id,
        transactionDate: e.TransactionDate,
        transactionType: e.TransactionType,
        fundId: e.FundId,
        orderId: e.OrderId,
        tradeId: e.TradeId,
        tradeType: e.TradeType,
        valueDate: e.ValueDate,
        tradeStatus: e.TradeStatus,
        description: e.Description,
        amount: e.Amount,
        balance: e.Balance)
    {
    }

    public FundTransaction SetBalance(decimal newBalance)
    {
        Balance = newBalance;
        return this;
    }
    public FundTransaction SetAmount(decimal newAmount)
    {
        Amount = newAmount;
        return this;
    }

    /// <summary>
    /// convert fund transaction to view model
    /// </summary>
    /// <returns></returns>
    public FundTransactionReadModel ToViewModel()
        => new  (
            transactionId: TransactionId.TransactionId,
            transactionDate: TransactionDate,
            transactionType: TransactionType,
            fundId: FundId,
            orderId: OrderId,
            tradeId: TradeId,
            tradeType: TradeType,
            valueDate: ValueDate,
            tradeStatus: TradeStatus,
            description: Description,
            amount: Amount,
            balance: Balance
        );
}

/// <summary>
/// fund transaction validator
/// </summary>
public class FundTransactionValidator : AbstractValidator<FundTransaction>
{
    public FundTransactionValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("FundTransaction.OrderId is zero or negative");
        RuleFor(x => x.FundId).GreaterThan(0).WithMessage("FundTransaction.FundId is zero or negative");
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("FundTransaction.TradeId is zero or negative");
    }
}
