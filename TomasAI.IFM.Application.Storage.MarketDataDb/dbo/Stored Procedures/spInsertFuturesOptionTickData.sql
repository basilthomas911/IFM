-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesOptionTickData] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@tickDate datetime,
	@tickTime bigint,
	@optionPrice real,
	@bidPrice real,
	@askPrice real,
	@bidSize int,
	@askSize int,
	@impliedVolatility real,
	@delta real,
	@gamma real,
	@vega real,
	@theta real,
	@rho real,
	@underlyingPrice real
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	insert into dbo.futures_option_tick_data(
		ContractId,
		TickDate,
		TickTime,
		OptionPrice,
		BidPrice,
		AskPrice,
		BidSize,
		AskSize,
		ImpliedVolatility,
		Delta,
		Gamma,
		Vega,
		Theta,
		Rho,
		UnderlyingPrice
	) values (
		@contractId,
		@tickDate,
		@tickTime,
		@optionPrice,
		@bidPrice,
		@askPrice,
		@bidSize,
		@askSize,
		@impliedVolatility,
		@delta,
		@gamma,
		@vega,
		@theta,
		@rho,
		@underlyingPrice
	)

END
