-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesTradeSignalLLMByDateRange] 
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
      ,[OpenPrice]
      ,[HighPrice]
      ,[LowPrice]
      ,[ClosePrice]
      ,[Volume]
      ,[DailyPercentChange]
      ,[DailyStdDev]
      ,[UpperBand]
      ,[Mean]
      ,[LowerBand]
      ,[PriceVolatility]
	  ,[CreatedOn]
	  ,[CreatedBy]
  FROM [dbo].[futures_trade_signal_llm]
  where ContractId = @contractId
  and ValueDate between @startDate and @endDate

END
