-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradePlanStopLossLimit] 
	@orderId int,
	@tradeId int
AS
BEGIN

	SELECT StopLossLimit from trade_plan
	where TradePlanId = (select max(TradePlanId) from trade_plan where OrderId = @orderId and TradeId = @tradeId)

END
