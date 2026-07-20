-- =============================================
-- Author:		basil thomas
-- Create date: april 15,2018
-- Description:	return futures contratc by contratc id
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesContract]
	@contractId varchar(64)
AS
BEGIN
	SET NOCOUNT ON;

	select
		ContractId,
		Description,
		Symbol,
		LocalSymbol,
		SecurityType,
		Currency,
		Exchange,
		Multiplier,
		LastTradeDate,
		CurrentlyTraded
	from dbo.futures_contract
	where ContractId = @contractId

END
