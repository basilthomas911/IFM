using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Configuration;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.Azure;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests
{

    public class AzureStorageTests
    {
        [Fact]
        public async Task AzureStorage_UploadFileAsync()
        {
            var curDir = Directory.GetCurrentDirectory();
            var config = new ConfigurationBuilder()
                .SetBasePath(curDir)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var storageOptions = config.GetSection("AppSettings:AzureStorage").Get<AzureStorageOptions>();
            var azureStorage = new AzureStorage(storageOptions);
            await azureStorage.UploadFileAsync("eventdb", "diff");


        }
    }
}
