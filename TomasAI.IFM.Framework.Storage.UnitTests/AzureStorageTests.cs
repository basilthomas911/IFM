using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Configuration;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.Azure;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{

    public class AzureStorageTests
    {
        [Fact]
        public void AzureStorageOptions_Ok()
        {
            var curDir = Directory.GetCurrentDirectory();
            var config = new ConfigurationBuilder()
                .SetBasePath(curDir)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var storageOptions = config.GetSection("AppSettings:AzureStorage").Get<AzureStorageOptions>();
            storageOptions.Should().NotBeNull();
            storageOptions.ConnectionString.Should().NotBeNullOrWhiteSpace();
            storageOptions.BackupFiles.Should().NotBeNull();
            storageOptions.BackupFiles.Should().HaveCountGreaterThan(0);
            storageOptions.GetStorageFile("eventdb", "diff").Should().NotBeNull();
            storageOptions.GetStorageFile("eventdb", "full").Should().NotBeNull();
            storageOptions.GetStorageFile("funddb", "diff").Should().NotBeNull();
            storageOptions.GetStorageFile("funddb", "full").Should().NotBeNull();
            storageOptions.GetStorageFile("logdb", "diff").Should().NotBeNull();
            storageOptions.GetStorageFile("logdb", "full").Should().NotBeNull();
            storageOptions.GetStorageFile("marketdatadb", "diff").Should().NotBeNull();
            storageOptions.GetStorageFile("marketdatadb", "full").Should().NotBeNull();
            storageOptions.GetStorageFile("optionpricerdb", "diff").Should().NotBeNull();
            storageOptions.GetStorageFile("optionpricerdb", "full").Should().NotBeNull();
            storageOptions.GetStorageFile("referencedb", "diff").Should().NotBeNull();
            storageOptions.GetStorageFile("referencedb", "full").Should().NotBeNull();
            storageOptions.GetStorageFile("tradedb", "diff").Should().NotBeNull();
            storageOptions.GetStorageFile("tradedb", "full").Should().NotBeNull();

        }
    }
}
