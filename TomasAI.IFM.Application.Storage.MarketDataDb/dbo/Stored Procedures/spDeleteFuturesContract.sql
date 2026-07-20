-- =============================================
-- Author:		basil thomas
-- Create date: april 15,2018
-- Description:	delete futures contract by contract id
-- =============================================
CREATE PROCEDURE spDeleteFuturesContract 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64)
AS
BEGIN
	delete futures_contract
	where ContractId = @contractId
END
