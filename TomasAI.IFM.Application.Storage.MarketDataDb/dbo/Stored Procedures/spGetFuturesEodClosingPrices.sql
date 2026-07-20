-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesEodClosingPrices]
	-- Add the parameters for the stored procedure here
	@symbol varchar(10),
	@startDate date,
	@endDate date, 
	@maxDays int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	SET ROWCOUNT @maxDays

	select 
		@symbol as Symbol,
		fed.ValueDate,
		fed.ClosePrice as ClosingPrice
	from dbo.futures_eod_data fed join futures_contract fc on fed.ContractId = fc.ContractId
	where fed.ValueDate between @startDate and @endDate
	and fc.Symbol = @symbol
	order by ValueDate desc

	SET ROWCOUNT 0

END
