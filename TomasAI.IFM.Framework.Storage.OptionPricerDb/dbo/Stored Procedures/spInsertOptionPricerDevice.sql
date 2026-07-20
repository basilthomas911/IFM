-- =============================================
-- Author:		Basil Thomas
-- Create date: Dec 17, 2017
-- Description:	insert option pricer device configuration
-- =============================================
CREATE PROCEDURE [dbo].[spInsertOptionPricerDevice]
	@deviceId int,
	@deviceName varchar(255),
	@spreadPaths int,
	@volatilityPaths int,
	@maxBatchSize int,
	@optionType varchar(32),
	@enabled bit
AS
	if (exists(select * from dbo.option_pricer_device
				where DeviceId = @deviceId))
		update dbo.option_pricer_device
		set DeviceName = @deviceName,
			SpreadPaths = @spreadPaths,
			VolatilityPaths = @volatilityPaths,
			MaxBatchSize = @maxBatchSize,
			OptionType = @optionType,
			[Enabled] = @enabled
		where DeviceId = @deviceId
	else
		insert into dbo.option_pricer_device (
			DeviceId,
			DeviceName,
			SpreadPaths,
			VolatilityPaths,
			MaxBatchSize,
			OptionType,
			[Enabled]
		) values (
			@deviceId,
			@deviceName,
			@spreadPaths,
			@volatilityPaths,
			@maxBatchSize,
			@optionType,
			@enabled
		)