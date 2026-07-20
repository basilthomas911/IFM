-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesItiSignalAverageTrendDelta] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(32),
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	Declare @maxValueDate date;

	select @maxValueDate = max(ValueDate) from futures_iti_signal where ValueDate < @valueDate 

    -- Insert statements for procedure here
	SELECT avg(TrendDelta) as AverageTrendDelta
	from futures_iti_signal
	where ContractId = @contractId
	and ValueDate = @maxValueDate

END
