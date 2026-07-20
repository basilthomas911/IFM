-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spGetFuturesOptionContract 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select 
		ContractId,
		Description,
		Symbol,
		LocalSymbol,
		SecurityType,
		Currency,
		Exchange,
		Multiplier,
		ContractMonth,
		StrikePrice,
		OptionType
	from dbo.futures_option_contract
	where ContractId = @contractId
END
