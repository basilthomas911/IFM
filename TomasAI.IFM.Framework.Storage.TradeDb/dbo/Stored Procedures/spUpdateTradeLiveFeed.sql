-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-09-19
-- Description:	update trade live feed
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateTradeLiveFeed]
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int,
	@liveFeed bit
AS
BEGIN

	update trade_live_feed
	set LiveFeed = @liveFeed
	where Orderid = @orderId
	and TradeId = @tradeId

END
