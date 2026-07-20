using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// Represents a fund order, including its identifying information, status, associated trades, and related operations.
/// </summary>
/// <remarks>This interface defines the contract for accessing and managing fund orders within the system.
/// Implementations may provide additional logic for order lifecycle management, such as closing orders or converting
/// them to view models for presentation. Thread safety and mutability depend on the specific implementation.</remarks>
public interface IFundOrder
{
    int OrderId { get;  }
    int FundId { get;  }
    string Reference { get; }
    OrderStatus OrderStatus { get; }
    DateTime CreatedOn { get; }
    string CreatedBy { get; }
    IFundOrderTradeCollection Trades { get; }

    FundOrderReadModel ToViewModel();
    void SetClosed();
}
