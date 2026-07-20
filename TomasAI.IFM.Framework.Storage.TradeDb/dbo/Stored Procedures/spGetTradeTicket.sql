-- =============================================
-- Author:		Basil Thomas
-- Create date: 2020-12-06
-- Description:	return trade ticket by primary key
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradeTicket]
	@fundId int,
	@orderId int,
	@tradeId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	select [FundId]
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
		  ,[UpdatedBy]
	  from [dbo].[trade_ticket]
	  where FundId = @fundId
	  and Orderid = @orderid
	  and TradeId = @tradeId

END

