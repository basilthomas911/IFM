-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetLastFuturesTickDataByTickDate] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@tickDate datetime
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	--declare @tickDate datetime
	--select @tickDate = (select max(TickDate) from dbo.futures_tick_data 
	--                   where ContractId = @contractId)

    -- Insert statements for procedure here
	select top 1
		ContractId,
		ValueDate,
		TickDate,
		TickTime,
		Price,
		Size
	from dbo.futures_tick_data
	where ContractId = @contractId
	and TickDate >= @tickDate

END
