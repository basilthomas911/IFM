-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateFuturesItiSignalTrendReversal] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(32),
	@valueDate date,
	@intrinsicTimeMode varchar(32),
	@trendReversal real
AS
BEGIN

	if exists(select max(SequenceId) from futures_iti_signal 
						  where ContractId = @contractId 
						  and ValueDate = @valueDate 
						  and IntrinsicTimeMode = @intrinsicTimeMode)
		update futures_iti_signal
		set TrendReversal = @trendReversal
		where SequenceId = (select max(SequenceId) from futures_iti_signal 
						  where ContractId = @contractId 
						  and ValueDate = @valueDate 
						  and IntrinsicTimeMode = @intrinsicTimeMode)
END
