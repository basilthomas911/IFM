using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests
{
    public class StringReaderTests
    {

        [Fact(Skip = "Requires a licensed HTTP data feed connection.")]
        public void ReadToEndAsyncOk()
        {
            var dataUri = new Uri("https://example.invalid/sanitized-yield-fixture.csv");
            var sr = new HttpStringReader(dataUri);
            var result = sr.ReadToEndAsync().Result;
            result.Should().NotBeNull();
        }

    }
}
