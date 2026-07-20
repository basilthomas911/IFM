-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
create PROCEDURE [dbo].[spGetFuturesOptionContractExists] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	if exists(select * from dbo.futures_option_contract where ContractId = @contractId)
		select cast(1 as bit) as FuturesOptionContractExists
	else
		select cast(0 as bit) as FuturesOptionContractExists

END
