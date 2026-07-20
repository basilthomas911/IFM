using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests
{
    public class StringReaderTests
    {

        [Fact]
        public void ReadToEndAsyncOk()
        {
            var dataUri = new Uri(@"https://www.quandl.com/api/v3/datasets/USTREASURY/YIELD.csv?api_key=Vpxxmo8BPMwZP-xH8XZZ");
            var sr = new HttpStringReader(dataUri);
            var result = sr.ReadToEndAsync().Result;
            result.Should().NotBeNull();
        }

    }
}
