-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetOptionPricerDevices] 
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT 
		opd.DeviceId,
		opd.DeviceName,
		opd.SpreadPaths,
		opd.VolatilityPaths,
		opd.MaxBatchSize,
		opd.OptionType,
		opd.Enabled
	FROM
		dbo.option_pricer_device opd
	where 
		opd.Enabled = 1

END
