-- =============================================
-- Author:		basil thomas
-- Create date: 2018-Nov-17
-- Description:	insert rate of return
-- =============================================
CREATE PROCEDURE spInsertRateOfReturn 
	-- Add the parameters for the stored procedure here
	@symbol varchar(50),
	@valueDate date,
	@rateOfReturn real
AS
BEGIN

    -- Insert statements for procedure here
	if exists(select * from return_rates where Symbol = @symbol and ValueDate = @valueDate)
		update return_rates
		set RateOfReturn = @rateOfReturn
		where Symbol = @symbol and ValueDate = @valueDate
	else
		insert into return_rates(
			Symbol,
			ValueDate,
			RateOfReturn
		) values (
			@symbol,
			@valueDate,
			@rateOfReturn
		)

END
