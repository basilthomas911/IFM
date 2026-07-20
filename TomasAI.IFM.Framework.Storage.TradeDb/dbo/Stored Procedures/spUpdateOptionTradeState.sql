-- =============================================
-- Author:		Basil Thomas
-- Create date: 2018-07-05
-- Description:	update trade state
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateOptionTradeState] 
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int,
	@tradeState varchar(32)
AS
BEGIN
	
	update dbo.option_trade
	set TradeState = @tradeState
	where OrderId = @orderId
	and TradeId = @tradeId

END
