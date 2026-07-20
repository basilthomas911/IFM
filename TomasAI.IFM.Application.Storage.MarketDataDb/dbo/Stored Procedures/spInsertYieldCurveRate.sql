-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertYieldCurveRate]

	@valueDate date,
	@oneMonth real,
	@twoMonth real,
	@threeMonth real,
	@sixMonth real,
	@oneYear real,
	@twoYear real,
	@threeYear real,
	@fiveYear real,
	@sevenYear real,
	@tenYear real,
	@twentyYear real,
	@thirtyYear real

AS
BEGIN

	delete from yield_curve_rates
	where ValueDate = @valueDate
		
	insert into [dbo].[yield_curve_rates]
           ([ValueDate]
           ,[OneMonth]
		   ,[TwoMonth]
           ,[ThreeMonth]
           ,[SixMonth]
           ,[OneYear]
           ,[TwoYear]
           ,[ThreeYear]
           ,[FiveYear]
           ,[SevenYear]
           ,[TenYear]
           ,[TwentyYear]
           ,[ThirtyYear])
     values
           (@valueDate
           ,@oneMonth
		   ,@twoMonth
           ,@threeMonth
           ,@sixMonth
           ,@oneYear
           ,@twoYear
           ,@threeYear
           ,@fiveYear
           ,@sevenYear
           ,@tenYear
           ,@twentyYear
           ,@thirtyYear)

END
