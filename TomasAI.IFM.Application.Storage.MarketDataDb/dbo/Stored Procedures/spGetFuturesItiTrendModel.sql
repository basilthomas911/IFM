-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesItiTrendModel] 
	-- Add the parameters for the stored procedure here
	@symbol varchar(10),
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT [Symbol]
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
      ,[ModelData]
	FROM [dbo].[futures_iti_trend_model]
	where Symbol = @symbol
	and ValueDate = (select max(ValueDate) 
	                 from futures_iti_trend_model
					 where Symbol = @symbol
					 and ValueDate <= @valueDate)

END
