-- =============================================
-- Author:		Basil Thomas
-- Create date: Dec 6, 2020
-- Description:	insert trade ticket
-- =============================================
CREATE PROCEDURE [dbo].[spInsertTradeTicket] 
	-- Add the parameters for the stored procedure here
	@fundId int,
	@orderId int,
	@tradeId int,
	@valueDate date,
	@tradeType varchar(32),
	@tradeSubType varchar(32),
	@tradeDate date,
	@maturityDate date,
	@tradeState varchar(32),
	@underlyingContractId varchar(64),
	@underlyingAssetType varchar(32),
	@orderDescription varchar(256),
	@orderAction varchar(32),
	@orderActionType varchar(32),
	@orderQuantity int,
	@orderType varchar(32),
	@orderPrice money,
	@orderAmount money,
	@commission money,
	@totalAmount money,
	@tradePnl money,
	@tradeFillType varchar(32),
	@createdOn datetime,
	@createdBy varchar(64),
	@updatedOn datetime,
	@updatedBy varchar(64)
AS
BEGIN

	if not exists(select * from trade_ticket where FundId = @fundId and OrderId = @orderId and TradeId = @tradeId)
		INSERT INTO [dbo].[trade_ticket]
				   ([FundId]
				   ,[OrderId]
				   ,[TradeId]
				   ,[ValueDate]
				   ,[TradeType]
				   ,[TradeSubType]
				   ,[TradeDate]
				   ,[MaturityDate]
				   ,[TradeState]
				   ,[UnderlyingContractId]
				   ,[UnderlyingAssetType]
				   ,[OrderDescription]
				   ,[OrderAction]
				   ,[OrderActionType]
				   ,[OrderQuantity]
				   ,[OrderType]
				   ,[OrderPrice]
				   ,[OrderAmount]
				   ,[Commission]
				   ,[TotalAmount]
				   ,[TradePnl]
				   ,[TradeFillType]
				   ,[CreatedOn]
				   ,[CreatedBy]
				   ,[UpdatedOn]
				   ,[UpdatedBy])
			 VALUES
				   (@fundId
				   ,@orderId
				   ,@tradeId
				   ,@valueDate
				   ,@tradeType
				   ,@tradeSubType
				   ,@tradeDate
				   ,@maturityDate
				   ,@tradeState
				   ,@underlyingContractId
				   ,@underlyingAssetType
				   ,@orderDescription
				   ,@orderAction
				   ,@orderActionType
				   ,@orderQuantity
				   ,@orderType
				   ,@orderPrice
				   ,@orderAmount
				   ,@commission
				   ,@totalAmount
				   ,@tradePnl
				   ,@tradeFillType
				   ,@createdOn
				   ,@createdBy
				   ,@updatedOn
				   ,@updatedBy)

END
