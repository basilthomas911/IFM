-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetLastFuturesTdiSignal] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT [ContractId]
      ,[ValueDate]
      ,[Timestamp]
      ,[UpTrendCount]
	  ,[DownTrendCount]
	  ,[TDI]
	  ,[TDIStrength]
	FROM [dbo].[futures_tdi_signal]
	where ContractId = @contractId 
	and ValueDate = @valueDate
	and Timestamp = (select max(Timestamp) from [dbo].[futures_tdi_signal]
					where ContractId = @contractId and ValueDate = @valueDate)

END
