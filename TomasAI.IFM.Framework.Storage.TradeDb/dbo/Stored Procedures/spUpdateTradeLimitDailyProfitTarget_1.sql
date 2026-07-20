-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateTradeLimitDailyProfitTarget]
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@tradeType varchar(32),
	@dailyProfitTarget money,
	@updatedOn datetime,
	@updatedBy varchar(64)
AS
BEGIN
	
	update dbo.trade_limit
	set DailyProfitTarget = @dailyProfitTarget,
		UpdatedOn = @updatedOn,
		UpdatedBy = @updatedBy
	where TradeId = @tradeId
	and TradeType = @tradeType
END