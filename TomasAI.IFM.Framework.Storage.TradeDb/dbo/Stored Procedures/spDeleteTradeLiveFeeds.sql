-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-10-09
-- Description:	delete all trade live feeds from order
-- =============================================
CREATE PROCEDURE [dbo].[spDeleteTradeLiveFeeds]
	-- Add the parameters for the stored procedure here
	@orderId int
AS
BEGIN

	delete from trade_live_feed
	where OrderId = @orderId 
END
