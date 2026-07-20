-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradePlanByDateRange] 
	-- Add the parameters for the stored procedure here
	@startDate datetime,
	@endDate datetime
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT [TradePlanId]
      ,[OrderId]
	  ,[TradeId]
	  ,[TradeType]
      ,[TradeDate]
      ,[ValueDate]
      ,[MaturityDate]
      ,[ActionDate]
      ,[ActionType]
      ,[ActionSubType]
      ,[ActionState]
      ,[ActionReason]
      ,[TradePnl]
	  ,[ForwardLossRatio]
      ,[LossProbability]
      ,[MScore]
      ,[MaxProfit]
      ,[MaxLoss]
      ,[MinProfitTarget]
      ,[DailyProfitTarget]
      ,[AssetPrice]
      ,[AssetStdDev]
      ,[AssetMean]
	  ,[AssetPriceChange]
      ,[MarketTrend]
      ,[MarketVolatility]
      ,[MarketDirection]
      ,[VixVolatility]
      ,[FiftyDayMA]
      ,[TwoHundredDayMA]
	  ,[PutSpreadProbability]
	  ,[CallSpreadProbability]
	  ,[NetPrice]
	  ,[ForwardPrice]
	  ,[StopLossLimit]
      ,[CreatedOn]
      ,[CreatedBy]
	FROM [dbo].[trade_plan]
	where ValueDate between @startDate and @endDate

END
