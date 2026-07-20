-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesItiTrendModel] 
	-- Add the parameters for the stored procedure here
	@symbol varchar(10), 
	@valueDate date,
	@startDate datetime,
	@endDate datetime,
	@count int,
	@maximum real,
	@mean real,
	@median real,
	@minimum real,
	@skewness real,
	@stdDev real,
	@variance real,
	@meanAbsoluteError real,
	@meanSquaredError real,
	@rootMeanSquaredError real,
	@lossFunction real,
	@rSquared real,
	@modelData varbinary(MAX)
AS
BEGIN

	delete from futures_iti_trend_model
	where Symbol = @symbol
	and ValueDate = @valueDate

	INSERT INTO [dbo].[futures_iti_trend_model]
           ([Symbol]
           ,[ValueDate]
           ,[StartDate]
           ,[EndDate]
           ,[Count]
           ,[Maximum]
           ,[Mean]
           ,[Median]
           ,[Minimum]
           ,[Skewness]
           ,[StdDev]
           ,[Variance]
           ,[MeanAbsoluteError]
		   ,[MeanSquaredError]
		   ,[RootMeanSquaredError]
		   ,[LossFunction]
		   ,[RSquared]
           ,[ModelData])
     VALUES
           (@symbol
           ,@valueDate
           ,@startDate
           ,@endDate
           ,@count
           ,@maximum
           ,@mean
           ,@median
           ,@minimum
           ,@skewness
           ,@stdDev
           ,@variance
		   ,@meanAbsoluteError
		   ,@meanSquaredError
		   ,@rootMeanSquaredError
		   ,@lossFunction
		   ,@rSquared
           ,@modelData)

END

