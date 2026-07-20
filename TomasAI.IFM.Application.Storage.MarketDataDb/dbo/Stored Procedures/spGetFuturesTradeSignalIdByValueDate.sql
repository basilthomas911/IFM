-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesTradeSignalIdByValueDate]
	-- Add the parameters for the stored procedure here
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT [ContractId]
      ,Max([ValueDate]) as ValueDate
    FROM [dbo].[futures_trade_signal]
    where ValueDate = @valueDate
	group by ContractId
END
