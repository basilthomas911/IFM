-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateFuturesItiSignalTrendExtreme] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(32),
	@valueDate date,
	@intrinsicTimeMode varchar(32),
	@trendExtreme real
AS
BEGIN

	if exists(select max(SequenceId) from futures_iti_signal 
						  where ContractId = @contractId 
						  and ValueDate = @valueDate 
						  and IntrinsicTimeMode = @intrinsicTimeMode)
		update futures_iti_signal
		set TrendExtreme = @trendExtreme,
			TrendDelta = @trendExtreme-TrendPrice
		where SequenceId = (select max(SequenceId) from futures_iti_signal 
						  where ContractId = @contractId 
						  and ValueDate = @valueDate 
						  and IntrinsicTimeMode = @intrinsicTimeMode)
END
