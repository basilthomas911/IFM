-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spDeleteFundOrderTrade2]
	-- Add the parameters for the stored procedure here
	@fundId int,
	@orderId int,
	@tradeid int
AS
BEGIN

	delete from dbo.fund_order_trade
	where FundId = @fundId 
	and OrderId = @orderId
	and TradeId = @tradeId

END
