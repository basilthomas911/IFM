-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetCurrentTradedFuturesContract] 
	-- Add the parameters for the stored procedure here
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT top 1 [ContractId]
      ,[Description]
      ,[Symbol]
      ,[LocalSymbol]
      ,[SecurityType]
      ,[Currency]
      ,[Exchange]
      ,[Multiplier]
      ,[LastTradeDate]
	  ,[CurrentlyTraded]
	FROM [dbo].[futures_contract]
	where CurrentlyTraded = 1

END
