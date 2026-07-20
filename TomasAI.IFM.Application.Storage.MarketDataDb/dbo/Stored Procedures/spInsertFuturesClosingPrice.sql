-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spInsertFuturesClosingPrice 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date,
	@closingPrice real,
	@createdOn datetime,
	@createdBy varchar(255)
AS
BEGIN

	delete from futures_closing_price 
	where ContractId = @contractId
	and ValueDate = @valueDate

	insert into futures_closing_price (
		ContractId,
		ValueDate,
		ClosingPrice,
		CreatedOn,
		CreatedBy
	) values (
		@contractId,
		@valueDate,
		@closingPrice,
		@createdOn,
		@createdBy
	)
END
