-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesEodMovingAverage]
	-- Add the parameters for the stored procedure here
	@symbol varchar(10),
	@startDate date,
	@endDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	select
		@symbol as Symbol,
		@endDate as ValueDate,
		AVG(fed.ClosePrice) as FuturesEodMovingAverage
	from dbo.futures_eod_data fed join futures_contract fc on fed.ContractId = fc.ContractId
	where fed.ValueDate between @startDate and @endDate
	and fc.Symbol = @symbol
END
