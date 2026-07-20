-- =============================================
-- Author:		Basil Thomas
-- Create date: 2020-12-07
-- Description:	update trade ticket order price
-- =============================================
CREATE PROCEDURE spUpdateTradeTicketOrderPrice 
	@fundId int,
	@orderId int,
	@tradeId int,
	@orderPrice money,
	@updatedOn datetime,
	@updatedBy varchar(64)
AS
BEGIN
	update trade_ticket
	set OrderPrice = @orderPrice,
		UpdatedOn = @updatedOn,
		UpdatedBy = @updatedBy
	where FundId = @fundId
	and Orderid = @orderId
	and TradeId = @tradeId
END
