-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spDeleteFundOrderTrade
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeid int
AS
BEGIN

	delete from dbo.fund_order_trade
	where OrderId = @orderId
	and TradeId = @tradeId

END
