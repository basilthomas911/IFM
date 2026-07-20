-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spDeleteFuturesOptionContract
	-- Add the parameters for the stored procedure here
	@contractId varchar(64)
AS
BEGIN

	delete from dbo.futures_option_contract
	where ContractId = @contractId
END
