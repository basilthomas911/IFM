-- =============================================
-- Author:		Basil Thomas
-- Create date: 2020-12-07
-- Description:	update trade ticket trade state
-- =============================================
CREATE PROCEDURE [spUpdateTradeTicketState] 
	-- Add the parameters for the stored procedure here
	@fundId int,
	@orderId int,
	@tradeid int,
	@tradeState varchar(32),
	@updatedOn datetime,
	@updatedBy varchar(64)
AS
BEGIN
	update trade_ticket
	set TradeState = @tradeState,
		UpdatedOn = @updatedOn,
		UpdatedBy = @updatedBy
	where FundId = @fundId
	and Orderid = @orderId
	and TradeId = @tradeId
END
