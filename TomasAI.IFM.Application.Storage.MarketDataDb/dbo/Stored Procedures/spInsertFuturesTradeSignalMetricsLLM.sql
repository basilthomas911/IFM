
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesTradeSignalMetricsLLM] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(32),
	@valueDate date,
	@timestamp datetime,
	@marketDirection varchar(32),
	@marketVolatility varchar(32),
	@priceDirection varchar(32),
	@priceVolatility varchar(32),
	@marketDirectionIndicator real,
	@createdOn datetime=null,
	@createdBy varchar(64)=null
AS
BEGIN

	delete from [dbo].[futures_trade_signal_metrics_llm]
	where ContractId = @contractId
	and ValueDate = @valueDate
	and Timestamp = @timeStamp

	INSERT INTO [dbo].[futures_trade_signal_metrics_llm]
           ([ContractId]
           ,[ValueDate]
           ,[Timestamp]
           ,[MarketDirection]
           ,[MarketVolatility]
           ,[PriceDirection]
           ,[PriceVolatility]
           ,[MarketDirectionIndicator]
		   ,[CreatedOn]
		   ,[CreatedBy])
     VALUES
           (@contractId
           ,@valueDate
           ,@timestamp
           ,@marketDirection
           ,@marketVolatility
           ,@priceDirection
           ,@priceVolatility
           ,@marketDirectionIndicator
		   ,@createdOn
		   ,@createdBy)

END
