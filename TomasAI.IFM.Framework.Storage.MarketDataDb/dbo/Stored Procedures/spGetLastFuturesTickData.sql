-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetLastFuturesTickData] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	--declare @tickDate datetime
	--select @tickDate = (select max(TickDate) from dbo.futures_tick_data 
	--                   where ContractId = @contractId)

    -- Insert statements for procedure here
	select
		ContractId,
		ValueDate,
		TickDate,
		TickTime,
		Price,
		Size
	from dbo.futures_tick_data
	where ContractId = @contractId
	--and TickDate = @tickDate
	and TickTime = (select max(TickTime) from dbo.futures_tick_data 
	                   where ContractId = @contractId )--and TickDate = @tickDate)

END
