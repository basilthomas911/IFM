-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetLastFuturesOptionTickData]
	-- Add the parameters for the stored procedure here
	@contractId varchar(128)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	declare @tickDate datetime
	select @tickDate = (select top 1 max(TickDate) from dbo.futures_option_tick_data where ContractId = @contractId)

    -- Insert statements for procedure here
	select 
	   [ContractId]
	  ,[TickDate]
      ,[TickTime]
      ,[OptionPrice]
      ,[BidPrice]
      ,[AskPrice]
      ,[BidSize]
      ,[AskSize]
      ,[ImpliedVolatility]
      ,[Delta]
      ,[Gamma]
      ,[Vega]
      ,[Theta]
	  ,[Rho]
      ,[UnderlyingPrice]
  from [dbo].[futures_option_tick_data]
  where ContractId = @contractId
  and TickDate = @tickDate
  and TickTime = (select top 1 max(TickTime) from dbo.futures_option_tick_data 
                  where ContractId = @contractId and TickDate = @tickDate)

END
