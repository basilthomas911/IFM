-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradeLimit] 
	-- Add the parameters for the stored procedure here
	@tradeid int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT tl.TradeId
      ,tl.TradeType
      ,tl.RiskMargin
      ,tl.MaxProfit
	  ,tl.MaxLoss
      ,tl.MaxReturn
      ,ttl.MaxLossLimit
      ,ttl.MinProfitLimit
      ,tl.MinProfitTarget
	  ,tl.DailyProfitTarget
	  ,tl.CreatedOn
	  ,tl.CreatedBy
	  ,tl.UpdatedOn
	  ,tl.UpdatedBy
	FROM dbo.trade_limit tl
	join dbo.trade_type_limit ttl on tl.TradeId = ttl.TradeId and tl.TradeType = ttl.TradeType
	where tl.Tradeid = @tradeId

END
