
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesTradeSignalMetricsLLMByDateRange] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(32),
	@startDate date,
	@endDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT [ContractId]
      ,[ValueDate]
      ,[Timestamp]
      ,[MarketDirection]
      ,[MarketVolatility]
      ,[PriceDirection]
      ,[PriceVolatility]
      ,[MarketDirectionIndicator]
	  ,[CreatedOn]
	  ,[CreatedBy]
  FROM [dbo].[futures_trade_signal_metrics_llm]
  where ContractId = @contractId
  and ValueDate between @startDate and @endDate

END
