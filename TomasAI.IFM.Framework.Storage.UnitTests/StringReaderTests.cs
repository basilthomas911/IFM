using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class StringReaderTests
    {

        [Fact]
        public void CreateStringReaderWithNullUri()
        {
            var act = () => { var sr = new HttpStringReader(default(Uri)); };
            act.Should().Throw<ArgumentNullException>();
        }

    }
}
