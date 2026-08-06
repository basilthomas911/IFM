using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        [Fact]
        public async Task ReadToEndAsync_WithCanceledToken_DoesNotStartHttpRequest()
        {
            var reader = new HttpStringReader(new Uri("https://example.invalid/data.csv"));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Func<Task> act = () => reader.ReadToEndAsync(cancellation.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

    }
}
