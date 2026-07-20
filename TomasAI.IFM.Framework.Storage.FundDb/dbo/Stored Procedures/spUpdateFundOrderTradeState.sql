-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spUpdateFundOrderTradeState 
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int,
	@tradeState varchar(32)
AS
BEGIN
	
	update dbo.fund_order_trade
	set TradeState = @tradeState
	where OrderId = @orderId
	and TradeId = @tradeId
END
