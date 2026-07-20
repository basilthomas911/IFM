-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradePlanSummary] 
	-- Add the parameters for the stored procedure here
	@valueDate date,
	@orderId int,
	@tradeId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT 
	   [OrderId]
	  ,[TradeId]
	  ,[TradeType]
      ,[TradeDate]
      ,[ValueDate]
      ,[MaturityDate]
      ,[ActionType]
      ,[ActionSubType]
      ,[ActionState]
      ,[MarketTrend]
      ,[MarketVolatility]
      ,[ActionDate]
      ,[ActionReason]
      ,[TradePnl]
	  ,[ForwardLossRatio]
      ,[LossProbability]
      ,[MScore]
      ,[AssetPrice]
      ,[AssetStdDev]
      ,[AssetMean]
	  ,[AssetPriceChange]
	  ,[NetPrice]
	  ,[ForwardPrice]
	  ,[StopLossLimit]
      ,[CreatedOn]
      ,[CreatedBy]
	FROM [dbo].[trade_plan_summary]
	where ValueDate = @valueDate 
	and OrderId = @orderId
	and TradeId = @tradeId
	Order By ActionDate desc


END
