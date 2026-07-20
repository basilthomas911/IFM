-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesBarData] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@symbol varchar(64),
	@valueDate date,
	@startDate datetime,
	@endDate datetime
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select 
		ContractId,
		Symbol,
		ValueDate,
		BarDate,
		BarRateType,
		BarValue,
		UpTrendTrigger,
		DownTrendTrigger
	from futures_bar_data
	where ContractId = @contractId
	and Symbol = @symbol
	--and ValueDate = @valueDate
	and BarDate between @startDate and @endDate

END
