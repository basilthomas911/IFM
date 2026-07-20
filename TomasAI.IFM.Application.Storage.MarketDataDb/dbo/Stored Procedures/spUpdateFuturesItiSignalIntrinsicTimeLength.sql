-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateFuturesItiSignalIntrinsicTimeLength] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(32),
	@valueDate date,
	@intrinsicTimeMode varchar(32),
	@intrinsicTimeLength real,
	@trendDelta real
AS
BEGIN

	if exists(select max(SequenceId) from futures_iti_signal 
						  where ContractId = @contractId 
						  and ValueDate = @valueDate 
						  and IntrinsicTimeMode = @intrinsicTimeMode)
		update futures_iti_signal
		set IntrinsicTimeLength = @intrinsicTimeLength,
			TrendDelta = @trendDelta
		where SequenceId = (select max(SequenceId) from futures_iti_signal 
						  where ContractId = @contractId 
						  and ValueDate = @valueDate 
						  and IntrinsicTimeMode = @intrinsicTimeMode)
END