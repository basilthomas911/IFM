-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesOptionContracts] 
	-- Add the parameters for the stored procedure here
	@symbol varchar(64)
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
	where Symbol = @symbol
	order by ContractMonth desc

END
