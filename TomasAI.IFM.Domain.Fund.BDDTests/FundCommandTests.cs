using FluentAssertions;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.State;
using TomasAI.IFM.Domain.Fund.Command.Validation;
using TomasAI.IFM.Domain.Fund.Command;

namespace TomasAI.IFM.Domain.Fund.BDDTests;

public class FundCommandTests
{
    [Fact]
    public void CreateFundCommand_GivenEmptyState_WhenExecuted_ThenReturnsFundCreatedEvent()
    {
        // Arrange - Given an empty state
        var state = new FundCommandState();
        state.FundExists.Should().BeFalse();

        var newFund = SampleData.Fund;

        var entityId = new FundId(newFund.FundId);
        var command = new CreateFundCommand(newFund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing CreateFundCommand
        var result = command!.Execute(state);

        // Assert - Then return FundCreatedEvent
        result.Success.Should().BeTrue();
        state.Events.Should().NotBeNullOrEmpty();
        state.Events.Should().ContainSingle(e => e is FundCreatedEvent);

        var fundCreatedEvent = state.Events.OfType<FundCreatedEvent>().First();
        fundCreatedEvent.Should().NotBeNull();
        fundCreatedEvent.NewFund.FundId.Should().Be(newFund.FundId);
        fundCreatedEvent.NewFund.Name.Should().Be(newFund.Name);
        fundCreatedEvent.NewFund.Balance.Should().Be(newFund.Balance);
        fundCreatedEvent.EntityId.Should().Be(command.EntityId);

        // Verify state was updated
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(newFund.FundId);
    }

    [Fact]
    public void CreateFundCommand_GivenExistingFund_WhenExecuted_ThenReturnsFailedResultWithFundAlreadyExistsMessage()
    {
        // Arrange - Given an existing fund
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        var newFund = SampleData.Fund with { FundId = 9999, Name = "NewFund" };

        var entityId = new FundId(newFund.FundId);
        var command = new CreateFundCommand(newFund)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing CreateFundCommand
        var result = command!.Execute(state);

        // Assert - Then return a failed result with a fund-already-exists error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"fundId {existingFund.FundId} already exists");
    }

    [Fact]
    public void AddOrderToFundCommand_GivenExistingFund_WhenExecuted_ThenReturnsOrderAddedToFundEvent()
    {
        // Arrange - Given an existing fund
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        var fundOrder = SampleData.FundOrder;

        var entityId = new FundId(fundOrder.FundId);
        var command = new AddOrderToFundCommand(fundOrder)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddOrderToFundCommand.Actor, AddOrderToFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing AddOrderToFundCommand
        var result = command!.Execute(state);

        // Assert - Then return OrderAddedToFundEvent
        result.Success.Should().BeTrue();
        state.Events.Should().NotBeNullOrEmpty();
        state.Events.Should().Contain(e => e is OrderAddedToFundEvent);

        var orderAddedEvent = state.Events.OfType<OrderAddedToFundEvent>().First();
        orderAddedEvent.Should().NotBeNull();
        orderAddedEvent.FundOrder.FundId.Should().Be(fundOrder.FundId);
        orderAddedEvent.FundOrder.OrderId.Should().Be(fundOrder.OrderId);
        orderAddedEvent.FundOrder.OrderStatus.Should().Be(fundOrder.OrderStatus);
        orderAddedEvent.EntityId.Should().Be(command.EntityId);

        // Verify state was updated
        state.FundOrderExists(fundOrder.FundId, fundOrder.OrderId).Should().BeTrue();
    }

    [Fact]
    public void AddOrderToFundCommand_GivenExistingFund_WhenAddingOrderWithNonExistingFundId_ThenReturnsFailedResultWithFundDoesNotExistMessage()
    {
        // Arrange - Given an existing fund
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Use fund order with non-existing fund ID
        var fundOrder = SampleData.FundOrderWithNonExistingFundId;
        fundOrder.FundId.Should().NotBe(existingFund.FundId);

        var entityId = new FundId(fundOrder.FundId);
        var command = new AddOrderToFundCommand(fundOrder)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddOrderToFundCommand.Actor, AddOrderToFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing AddOrderToFundCommand with non-existing fund ID
        var result = command!.Execute(state);

        // Assert - Then return a failed result with a fund-does-not-exist error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"fundId {fundOrder.FundId} does not exist");
    }

    [Fact]
    public void AddOrderToFundCommand_GivenExistingFundOrder_WhenAddingDuplicateOrder_ThenReturnsFailedResultWithOrderAlreadyExistsMessage()
    {
        // Arrange - Given an existing fund with an order
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Create command with the same order (duplicate)
        var entityId = new FundId(existingOrder.FundId);
        var command = new AddOrderToFundCommand(existingOrder)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddOrderToFundCommand.Actor, AddOrderToFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing AddOrderToFundCommand with duplicate order
        var result = command!.Execute(state);

        // Assert - Then return a failed result with an order-already-exists error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"orderId {existingOrder.OrderId} already exists");
    }

    [Fact]
    public void AddOrderToFundCommand_GivenExistingFund_WhenAddingOrderWithNonOpenOrderStatus_ThenReturnsFailedResultWithInvalidOrderStatusMessage()
    {
        // Arrange - Given an existing fund
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Use fund order with closed order status (non-open)
        var fundOrder = SampleData.FundOrderWithClosedOrderStatus;
        fundOrder.OrderStatus.Should().NotBe(Shared.OrderStatus.Open);

        var entityId = new FundId(fundOrder.FundId);
        var command = new AddOrderToFundCommand(fundOrder)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddOrderToFundCommand.Actor, AddOrderToFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing AddOrderToFundCommand with non-open order status
        var result = command!.Execute(state);

        // Assert - Then return a failed result with an invalid-order-status error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"orderId {fundOrder.OrderId} invalid order status");
    }

    [Fact]
    public void AddTradeToFundOrderCommand_GivenExistingFundOrderTrade_WhenAddingTradeToFundOrder_ThenReturnsTradeAddedToFundOrderEvent()
    {
        // Arrange - Given an existing fund order with an existing trade
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;
        var existingTrade = SampleData.FundOrderTrade;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Apply TradeAddedToFundOrderEvent to simulate an existing trade
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = existingTrade }).Should().BeTrue();
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeTrue();

        // Create a new trade with a different tradeId
        var newTrade = SampleData.FundOrderTrade with { TradeId = existingTrade.TradeId + 1 };

        var entityId = new FundId(newTrade.FundId);
        var command = new AddTradeToFundOrderCommand(newTrade)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddTradeToFundOrderCommand.Actor, AddTradeToFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing AddTradeToFundOrderCommand
        var result = command!.Execute(state);

        // Assert - Then return TradeAddedToFundOrderEvent
        result.Success.Should().BeTrue();
        state.Events.Should().NotBeNullOrEmpty();
        state.Events.Should().Contain(e => e is TradeAddedToFundOrderEvent);

        var tradeAddedEvent = state.Events.OfType<TradeAddedToFundOrderEvent>().Last();
        tradeAddedEvent.Should().NotBeNull();
        tradeAddedEvent.FundOrderTrade.FundId.Should().Be(newTrade.FundId);
        tradeAddedEvent.FundOrderTrade.OrderId.Should().Be(newTrade.OrderId);
        tradeAddedEvent.FundOrderTrade.TradeId.Should().Be(newTrade.TradeId);
        tradeAddedEvent.EntityId.Should().Be(command.EntityId);

        // Verify state was updated
        state.FundOrderTradeExists(newTrade.FundId, newTrade.OrderId, newTrade.TradeId).Should().BeTrue();
    }

    [Fact]
    public void AddTradeToFundOrderCommand_GivenExistingFundOrderTrade_WhenAddingDuplicateTrade_ThenReturnsFailedResultWithTradeAlreadyExistsMessage()
    {
        // Arrange - Given an existing fund order with an existing trade
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;
        var existingTrade = SampleData.FundOrderTrade;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Apply TradeAddedToFundOrderEvent to simulate an existing trade
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = existingTrade }).Should().BeTrue();
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeTrue();

        // Create command with the same trade (duplicate)
        var entityId = new FundId(existingTrade.FundId);
        var command = new AddTradeToFundOrderCommand(existingTrade)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddTradeToFundOrderCommand.Actor, AddTradeToFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing AddTradeToFundOrderCommand with duplicate trade
        var result = command!.Execute(state);

        // Assert - Then return a failed result with a trade-already-exists error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"tradeId {existingTrade.TradeId} already exists");
    }

    [Fact]
    public void AddTradeToFundOrderCommand_GivenExistingFundOrderTrade_WhenAddingTradeWithNonExistingFundId_ThenReturnsFailedResultWithFundDoesNotExistMessage()
    {
        // Arrange - Given an existing fund order with an existing trade
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;
        var existingTrade = SampleData.FundOrderTrade;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Apply TradeAddedToFundOrderEvent to simulate an existing trade
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = existingTrade }).Should().BeTrue();
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeTrue();

        // Use fund order trade with non-existing fund ID
        var fundOrderTrade = SampleData.FundOrderTradeWithNonExistingFundId;
        fundOrderTrade.FundId.Should().NotBe(existingFund.FundId);

        var entityId = new FundId(fundOrderTrade.FundId);
        var command = new AddTradeToFundOrderCommand(fundOrderTrade)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddTradeToFundOrderCommand.Actor, AddTradeToFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing AddTradeToFundOrderCommand with non-existing fund ID
        var result = command!.Execute(state);

        // Assert - Then return a failed result with a fund-does-not-exist error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"fundId {fundOrderTrade.FundId} does not exist");
    }

    [Fact]
    public void AddTradeToFundOrderCommand_GivenExistingFundOrderTrade_WhenAddingTradeWithNonExistingOrderId_ThenReturnsFailedResultWithOrderDoesNotExistMessage()
    {
        // Arrange - Given an existing fund order with an existing trade
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;
        var existingTrade = SampleData.FundOrderTrade;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Apply TradeAddedToFundOrderEvent to simulate an existing trade
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = existingTrade }).Should().BeTrue();
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeTrue();

        // Use fund order trade with non-existing order ID
        var fundOrderTrade = SampleData.FundOrderTradeWithNonExistingOrderId;
        fundOrderTrade.OrderId.Should().NotBe(existingOrder.OrderId);

        var entityId = new FundId(fundOrderTrade.FundId);
        var command = new AddTradeToFundOrderCommand(fundOrderTrade)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddTradeToFundOrderCommand.Actor, AddTradeToFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing AddTradeToFundOrderCommand with non-existing order ID
        var result = command!.Execute(state);

        // Assert - Then return a failed result with an order-does-not-exist error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"orderId {fundOrderTrade.OrderId} does not exist");
    }

    [Fact]
    public void AddTradeToFundOrderCommand_GivenExistingFundOrderTrade_WhenAddingTradeWithMinimumTradeDate_ThenThrowsAddTradeToFundOrderException()
    {
        // Arrange - Given an existing fund order with an existing trade
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;
        var existingTrade = SampleData.FundOrderTrade;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Apply TradeAddedToFundOrderEvent to simulate an existing trade
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = existingTrade }).Should().BeTrue();
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeTrue();

        // Use fund order trade with minimum trade date (invalid)
        var fundOrderTrade = SampleData.FundOrderTradeWithMinimumTradeDate;
        fundOrderTrade.TradeDate.Should().Be(DateOnly.MinValue);

        var entityId = new FundId(fundOrderTrade.FundId);
        var command = new AddTradeToFundOrderCommand(fundOrderTrade)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, AddTradeToFundOrderCommand.Actor, AddTradeToFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When validating the AddTradeToFundOrderCommand with minimum trade date
        var validationErrors = new List<ValidationError>()
            .ValidateCommandId(command!.CommandId, command.CommandName)
            .ValidateFundOrderTrade(command.FundOrderTrade);

        // Assert - Then throw CommandValidationException with message about invalid TradeDate
        Action act = () => validationErrors.ThrowCommandValidationExceptionOnAnyError(command.ErrorCode);

        act.Should().Throw<CommandValidationException>()
            .WithMessage($"*TradeDate is invalid*");
    }

    [Fact]
    public void ChangeFundOrderTradeStateCommand_GivenExistingFundOrderTrade_WhenChangingFundOrderTradeState_ThenReturnsFundOrderTradeStateChangedEvent()
    {
        // Arrange - Given an existing fund order with an existing trade
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;
        var existingTrade = SampleData.FundOrderTrade;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Apply TradeAddedToFundOrderEvent to simulate an existing trade
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = existingTrade }).Should().BeTrue();
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeTrue();

        // Create command to change fund order trade state
        var fundOrderTradeId = new FundOrderTradeId(existingFund.FundId, existingOrder.OrderId, existingTrade.TradeId);
        var newTradeState = TradeState.TradeToOpen;

        var entityId = new FundId(fundOrderTradeId.FundId);
        var command = new ChangeFundOrderTradeStateCommand(fundOrderTradeId, newTradeState)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ChangeFundOrderTradeStateCommand.Actor, ChangeFundOrderTradeStateCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing ChangeFundOrderTradeStateCommand
        var result = command!.Execute(state);

        // Assert - Then return FundOrderTradeStateChangedEvent
        result.Success.Should().BeTrue();
        state.Events.Should().NotBeNullOrEmpty();
        state.Events.Should().Contain(e => e is FundOrderTradeStateChangedEvent);

        var tradeStateChangedEvent = state.Events.OfType<FundOrderTradeStateChangedEvent>().Last();
        tradeStateChangedEvent.Should().NotBeNull();
        tradeStateChangedEvent.FundOrderTradeId.FundId.Should().Be(fundOrderTradeId.FundId);
        tradeStateChangedEvent.FundOrderTradeId.OrderId.Should().Be(fundOrderTradeId.OrderId);
        tradeStateChangedEvent.FundOrderTradeId.TradeId.Should().Be(fundOrderTradeId.TradeId);
        tradeStateChangedEvent.TradeState.Should().Be(newTradeState);
        tradeStateChangedEvent.EntityId.Should().Be(command.EntityId);
    }

    [Fact]
    public void ChangeFundOrderTradeStateCommand_GivenExistingFundOrderTrade_WhenChangingFundOrderTradeStateWithNonExistingFundId_ThenReturnsFailedResultWithFundDoesNotExistMessage()
    {
        // Arrange - Given an existing fund order with an existing trade
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;
        var existingTrade = SampleData.FundOrderTrade;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Apply TradeAddedToFundOrderEvent to simulate an existing trade
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = existingTrade }).Should().BeTrue();
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeTrue();

        // Create command with non-existing fund ID
        var nonExistingFundId = 9999;
        nonExistingFundId.Should().NotBe(existingFund.FundId);
        var fundOrderTradeId = new FundOrderTradeId(nonExistingFundId, existingOrder.OrderId, existingTrade.TradeId);
        var newTradeState = TradeState.TradeToOpen;

        var entityId = new FundId(fundOrderTradeId.FundId);
        var command = new ChangeFundOrderTradeStateCommand(fundOrderTradeId, newTradeState)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ChangeFundOrderTradeStateCommand.Actor, ChangeFundOrderTradeStateCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing ChangeFundOrderTradeStateCommand with non-existing fund ID
        var result = command!.Execute(state);

        // Assert - Then return a failed result with a fund-does-not-exist error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"fundId {nonExistingFundId} does not exist");
    }

    [Fact]
    public void ChangeFundOrderTradeStateCommand_GivenExistingFundOrderTrade_WhenChangingFundOrderTradeStateWithNonExistingOrderId_ThenReturnsFailedResultWithOrderDoesNotExistMessage()
    {
        // Arrange - Given an existing fund order with an existing trade
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;
        var existingTrade = SampleData.FundOrderTrade;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Apply TradeAddedToFundOrderEvent to simulate an existing trade
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = existingTrade }).Should().BeTrue();
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeTrue();

        // Create command with non-existing order ID
        var nonExistingOrderId = 9999;
        nonExistingOrderId.Should().NotBe(existingOrder.OrderId);
        var fundOrderTradeId = new FundOrderTradeId(existingFund.FundId, nonExistingOrderId, existingTrade.TradeId);
        var newTradeState = TradeState.TradeToOpen;

        var entityId = new FundId(fundOrderTradeId.FundId);
        var command = new ChangeFundOrderTradeStateCommand(fundOrderTradeId, newTradeState)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ChangeFundOrderTradeStateCommand.Actor, ChangeFundOrderTradeStateCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing ChangeFundOrderTradeStateCommand with non-existing order ID
        var result = command!.Execute(state);

        // Assert - Then return a failed result with an order-does-not-exist error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"orderId {nonExistingOrderId} does not exist");
    }

    [Fact]
    public void ChangeFundOrderTradeStateCommand_GivenExistingFundOrderTrade_WhenChangingFundOrderTradeStateWithNonExistingTradeId_ThenReturnsFailedResultWithTradeDoesNotExistMessage()
    {
        // Arrange - Given an existing fund order with an existing trade
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;
        var existingTrade = SampleData.FundOrderTrade;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Apply TradeAddedToFundOrderEvent to simulate an existing trade
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = existingTrade }).Should().BeTrue();
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeTrue();

        // Create command with non-existing trade ID
        var nonExistingTradeId = 9999;
        nonExistingTradeId.Should().NotBe(existingTrade.TradeId);
        var fundOrderTradeId = new FundOrderTradeId(existingFund.FundId, existingOrder.OrderId, nonExistingTradeId);
        var newTradeState = TradeState.TradeToOpen;

        var entityId = new FundId(fundOrderTradeId.FundId);
        var command = new ChangeFundOrderTradeStateCommand(fundOrderTradeId, newTradeState)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ChangeFundOrderTradeStateCommand.Actor, ChangeFundOrderTradeStateCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing ChangeFundOrderTradeStateCommand with non-existing trade ID
        var result = command!.Execute(state);

        // Assert - Then return a failed result with a trade-does-not-exist error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"tradeId {nonExistingTradeId} does not exist");
    }

    [Fact]
    public void RemoveOrderFromFundCommand_GivenExistingFundOrder_WhenRemovingOrderFromFund_ThenReturnsOrderRemovedFromFundEvent()
    {
        // Arrange - Given an existing fund with an order
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Create command to remove the order from the fund
        var fundOrderId = new FundOrderId(existingFund.FundId, existingOrder.OrderId);

        var entityId = new FundId(fundOrderId.FundId);
        var command = new RemoveOrderFromFundCommand(fundOrderId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, RemoveOrderFromFundCommand.Actor, RemoveOrderFromFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing RemoveOrderFromFundCommand
        var result = command!.Execute(state);

        // Assert - Then return OrderRemovedFromFundEvent
        result.Success.Should().BeTrue();
        state.Events.Should().NotBeNullOrEmpty();
        state.Events.Should().Contain(e => e is OrderRemovedFromFundEvent);

        var orderRemovedEvent = state.Events.OfType<OrderRemovedFromFundEvent>().Last();
        orderRemovedEvent.Should().NotBeNull();
        orderRemovedEvent.FundOrderId.FundId.Should().Be(fundOrderId.FundId);
        orderRemovedEvent.FundOrderId.OrderId.Should().Be(fundOrderId.OrderId);
        orderRemovedEvent.EntityId.Should().Be(command.EntityId);
    }

    [Fact]
    public void RemoveOrderFromFundCommand_GivenExistingFundOrder_WhenRemovingOrderFromFundWithNonExistingFundId_ThenReturnsFailedResultWithFundDoesNotExistMessage()
    {
        // Arrange - Given an existing fund with an order
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Create command with non-existing fund ID
        var fundOrderIdWithNonExistingFundId = SampleData.FundOrderIdWithNonExistingFundId;
        fundOrderIdWithNonExistingFundId.FundId.Should().NotBe(existingFund.FundId);

        var entityId = new FundId(fundOrderIdWithNonExistingFundId.FundId);
        var command = new RemoveOrderFromFundCommand(fundOrderIdWithNonExistingFundId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, RemoveOrderFromFundCommand.Actor, RemoveOrderFromFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing RemoveOrderFromFundCommand with non-existing fund ID
        var result = command!.Execute(state);

        // Assert - Then return a failed result with a fund-does-not-exist error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"fundId {fundOrderIdWithNonExistingFundId.FundId} does not exist");
    }

    [Fact]
    public void RemoveOrderFromFundCommand_GivenExistingFundOrder_WhenRemovingOrderFromFundWithNonExistingOrderId_ThenReturnsFailedResultWithOrderDoesNotExistMessage()
    {
        // Arrange - Given an existing fund with an order
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Create command with non-existing order ID
        var fundOrderIdWithNonExistingOrderId = SampleData.FundOrderIdWithNonExistingOrderId;
        fundOrderIdWithNonExistingOrderId.OrderId.Should().NotBe(existingOrder.OrderId);

        var entityId = new FundId(fundOrderIdWithNonExistingOrderId.FundId);
        var command = new RemoveOrderFromFundCommand(fundOrderIdWithNonExistingOrderId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, RemoveOrderFromFundCommand.Actor, RemoveOrderFromFundCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing RemoveOrderFromFundCommand with non-existing order ID
        var result = command!.Execute(state);

        // Assert - Then return a failed result with an order-does-not-exist error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"orderId {fundOrderIdWithNonExistingOrderId.OrderId} does not exist");
    }

    [Fact]
    public void RemoveTradeFromFundOrderCommand_GivenExistingFundOrderTrade_WhenRemovingTradeFromFundOrder_ThenReturnsTradeRemovedFromFundOrderEvent()
    {
        // Arrange - Given an existing fund order with an existing trade
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;
        var existingTrade = SampleData.FundOrderTrade;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Apply TradeAddedToFundOrderEvent to simulate an existing trade
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = existingTrade }).Should().BeTrue();
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeTrue();

        // Create command to remove the trade from the fund order
        var fundOrderTradeId = new FundOrderTradeId(existingFund.FundId, existingOrder.OrderId, existingTrade.TradeId);

        var entityId = new FundId(fundOrderTradeId.FundId);
        var command = new RemoveTradeFromFundOrderCommand(fundOrderTradeId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, RemoveTradeFromFundOrderCommand.Actor, RemoveTradeFromFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing RemoveTradeFromFundOrderCommand
        var result = command!.Execute(state);

        // Assert - Then return TradeRemovedFromFundOrderEvent
        result.Success.Should().BeTrue();
        state.Events.Should().NotBeNullOrEmpty();
        state.Events.Should().Contain(e => e is TradeRemovedFromFundOrderEvent);

        var tradeRemovedEvent = state.Events.OfType<TradeRemovedFromFundOrderEvent>().Last();
        tradeRemovedEvent.Should().NotBeNull();
        tradeRemovedEvent.FundOrderTradeId.FundId.Should().Be(fundOrderTradeId.FundId);
        tradeRemovedEvent.FundOrderTradeId.OrderId.Should().Be(fundOrderTradeId.OrderId);
        tradeRemovedEvent.FundOrderTradeId.TradeId.Should().Be(fundOrderTradeId.TradeId);
        tradeRemovedEvent.EntityId.Should().Be(command.EntityId);

        // Verify state was updated - trade should no longer exist
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeFalse();
    }

    [Fact]
    public void RemoveTradeFromFundOrderCommand_GivenExistingFundOrderTrade_WhenRemovingTradeFromFundOrderWithNonExistingFundId_ThenReturnsFailedResultWithFundDoesNotExistMessage()
    {
        // Arrange - Given an existing fund order with an existing trade
        var state = new FundCommandState();
        
        var existingFund = SampleData.Fund;
        var existingOrder = SampleData.FundOrder;
        var existingTrade = SampleData.FundOrderTrade;

        // Apply FundCreatedEvent to simulate an existing fund
        state.Apply(new FundCreatedEvent { NewFund = existingFund }).Should().BeTrue();
        state.FundExists.Should().BeTrue();
        state.FundId.Should().Be(existingFund.FundId);

        // Apply OrderAddedToFundEvent to simulate an existing order
        state.Apply(new OrderAddedToFundEvent { FundOrder = existingOrder }).Should().BeTrue();
        state.FundOrderExists(existingOrder.FundId, existingOrder.OrderId).Should().BeTrue();

        // Apply TradeAddedToFundOrderEvent to simulate an existing trade
        state.Apply(new TradeAddedToFundOrderEvent { FundOrderTrade = existingTrade }).Should().BeTrue();
        state.FundOrderTradeExists(existingTrade.FundId, existingTrade.OrderId, existingTrade.TradeId).Should().BeTrue();

        // Create command with non-existing fund ID
        var fundOrderTradeIdWithNonExistingFundId = SampleData.FundOrderTradeIdWithNonExistingFundId;
        fundOrderTradeIdWithNonExistingFundId.FundId.Should().NotBe(existingFund.FundId);

        var entityId = new FundId(fundOrderTradeIdWithNonExistingFundId.FundId);
        var command = new RemoveTradeFromFundOrderCommand(fundOrderTradeIdWithNonExistingFundId)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, RemoveTradeFromFundOrderCommand.Actor, RemoveTradeFromFundOrderCommand.Verb, entityId.Format()),
            EntityId = entityId
        };

        command.Should().NotBeNull();

        // Act - When executing RemoveTradeFromFundOrderCommand with non-existing fund ID
        var result = command!.Execute(state);

        // Assert - Then return a failed result with a fund-does-not-exist error message, and no exception thrown
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"fundId {fundOrderTradeIdWithNonExistingFundId.FundId} does not exist");
    }
}