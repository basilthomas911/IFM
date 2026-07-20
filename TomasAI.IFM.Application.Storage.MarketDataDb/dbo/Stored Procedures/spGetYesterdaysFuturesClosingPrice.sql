-- =============================================
-- Author:		basil thomas
-- Create date: 2022-02-16
-- Description:	return yesterdays futures closing price
-- =============================================
CREATE PROCEDURE spGetYesterdaysFuturesClosingPrice
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	select top 1
		ContractId,
		ValueDate,
		ClosingPrice,
		CreatedOn,
		CreatedBy
	from futures_closing_price 
	where ContractId = @contractId 
	and ValueDate < @valueDate
	order by ValueDate desc

END
