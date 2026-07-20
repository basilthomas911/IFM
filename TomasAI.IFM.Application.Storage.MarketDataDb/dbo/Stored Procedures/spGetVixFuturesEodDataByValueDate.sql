-- =============================================
-- Author:		Basil Thomas
-- Create date: 2020-11-18
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[spGetVixFuturesEodDataByValueDate]
	-- Add the parameters for the stored procedure here
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select 
	   [ContractId]
      ,[ValueDate]
      ,[OpenPrice]
      ,[HighPrice]
      ,[LowPrice]
      ,[ClosePrice]
      ,[Volume]
	from [dbo].[vix_futures_eod_data]
	where ValueDate <= @valueDate
	order by ValueDate desc

END
