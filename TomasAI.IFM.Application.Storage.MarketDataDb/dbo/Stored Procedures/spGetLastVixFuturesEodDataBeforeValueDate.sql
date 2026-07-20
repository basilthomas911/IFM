-- =============================================
-- Author:		Basil Thomas
-- Create date: 2020-11-18
-- Description:	
-- =============================================
Create PROCEDURE [dbo].[spGetLastVixFuturesEodDataBeforeValueDate]
	-- Add the parameters for the stored procedure here
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select top 1
	   [ContractId]
      ,[ValueDate]
      ,[OpenPrice]
      ,[HighPrice]
      ,[LowPrice]
      ,[ClosePrice]
      ,[Volume]
	from [dbo].[vix_futures_eod_data]
	where ValueDate < @valueDate
	order by ValueDate desc

END
