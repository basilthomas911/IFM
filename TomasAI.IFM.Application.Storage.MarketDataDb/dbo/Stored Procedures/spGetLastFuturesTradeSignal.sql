-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetLastFuturesTradeSignal]
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT [ContractId]
      ,[ValueDate]
      ,[StdDev]
      ,[FuturesPrice]
	  ,[PriceChangePercent]
      ,[FundRiskPercent]
      ,[RSI]
	  ,[RSISlope]
      ,[TrendType]
      ,[TrendStrength]
      ,[TradeSignal]
      ,[TDI]
	  ,[TDIStrength]
	  ,[MDI]
	  ,[MDIWatermark]
	  ,[UpTrendingTrigger]
	  ,[DownTrendingTrigger]
	  ,[EntryTrigger]
	  ,[ExitTrigger]
	  ,[TrendDelta]
	  ,[TrendExtreme]
	  ,[TrendReversal]
	  ,[FiftyDMA]
	  ,[TwoHundredDMA]
	  ,[TradeExecuteState]
      ,[CreatedOn]
      ,[CreatedBy]
  FROM [dbo].[futures_trade_signal]
  where SequenceId = (select max(SequenceId) from [dbo].[futures_trade_signal]
					where ContractId = @contractId and ValueDate = @valueDate)

END
