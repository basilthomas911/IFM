using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Caching;

namespace TomasAI.IFM.Shared.UnitTests.Caching
{
    public class DataCacheServiceTests
    {
        private class TestItem
        {
            public string Name { get; set; }
            public string Address { get; set; }
        }

        [Fact]
        public void AddOk()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent"};

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);

            // then...
            dcs.Count.Should().Be(1);
            var testItemOut = dcs.Get<string, TestItem>(DataCacheName.boundedContextState, "TestKey");
            testItemOut.Should().NotBeNull();
            testItemOut.Name.Should().Be(testItem.Name);
            testItemOut.Address.Should().Be(testItem.Address);
        }

        [Fact]
        public void AddWithUndefinedCacheName()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            Action thenAction = () =>  dcs.Add<string, TestItem>(DataCacheName.Undefined, "TestKey", testItem);

            // then...
            thenAction.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void AddWithNullCacheKey()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            Action thenAction = () => dcs.Add<string, TestItem>(DataCacheName.boundedContextState, null, testItem);

            // then...
            thenAction.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void AddWithNullCacheItem()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = default(TestItem);

            // when..
            Action thenAction = () => dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);

            // then...
            thenAction.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ExistsOk()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);

            // then...
            dcs.Count.Should().Be(1);
            dcs.Exists<string>(DataCacheName.boundedContextState, "TestKey").Should().BeTrue();
        }

        [Fact]
        public void ExistsWithNonExistingCache()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);

            // then...
            dcs.Count.Should().Be(1);
            dcs.Exists<string>(DataCacheName.FuturesEodData, "TestKey").Should().BeFalse();
        }

        [Fact]
        public void ExistsWithUndefinedCacheName()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            Action thenAction = () => dcs.Exists<string>(DataCacheName.Undefined, "TestKey").Should().BeTrue();

            // then...
            thenAction.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ExistsWithNullCacheKey()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            Action thenAction = () => dcs.Exists<string>(DataCacheName.boundedContextState, null).Should().BeTrue();

            // then...
            thenAction.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetOk()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            var testItemOut = dcs.Get<string, TestItem>(DataCacheName.boundedContextState, "TestKey");

            // then...
            dcs.Count.Should().Be(1);
            dcs.Exists<string>(DataCacheName.boundedContextState, "TestKey").Should().BeTrue();
            testItemOut.Should().NotBeNull();
            testItemOut.Name.Should().Be(testItem.Name);
            testItemOut.Address.Should().Be(testItem.Address);
        }

        [Fact]
        public void GetWithUndefinedCacheName()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            Action thenAction = () => dcs.Get<string, TestItem>(DataCacheName.Undefined, "TestKey");

            // then...
            thenAction.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetWithNullCacheKey()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            Action thenAction = () => dcs.Get<string, TestItem>(DataCacheName.boundedContextState, null);

            // then...
            thenAction.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetWithNonExistingCache()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            Action thenAction = () => dcs.Get<string, TestItem>(DataCacheName.StopLossLimit, "TestKey");

            // then...
            thenAction.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void GetWithNonExistingCacheKey()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            Action thenAction = () => dcs.Get<string, TestItem>(DataCacheName.boundedContextState, "NoTestKey");

            // then...
            thenAction.Should().Throw<InvalidOperationException>();
        }


        [Fact]
        public void RemoveOk()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            dcs.Remove<string>(DataCacheName.boundedContextState, "TestKey");

            // then...
            dcs.Count.Should().Be(1);
            dcs.Exists<string>(DataCacheName.boundedContextState, "TestKey").Should().BeFalse();
        }

        [Fact]
        public void RemoveWithUndefinedCacheName()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            Action thenAction = () => dcs.Remove<string>(DataCacheName.Undefined, "NoTestKey");

            // then...
            thenAction.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void RemoveWithNonExistingCache()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            Action thenAction = () => dcs.Remove<string>(DataCacheName.ForwardLossRatioMap, "TestKey");

            // then...
            thenAction.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void RemoveWithNullCacheKey()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            Action thenAction = () => dcs.Remove<string>(DataCacheName.boundedContextState, null);

            // then...
            thenAction.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void RemoveWithNonExistingCacheKey()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            Action thenAction = () => dcs.Remove<string>(DataCacheName.boundedContextState, "NoTestKey");

            // then...
            thenAction.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void RemoveCacheOk()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            dcs.Remove<string>(DataCacheName.boundedContextState);

            // then...
            dcs.Count.Should().Be(0);
            dcs.Exists<string>(DataCacheName.boundedContextState, "TestKey").Should().BeFalse();
        }

        [Fact]
        public void RemoveCacheWithNonExistingCacheName()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            Action thenAction = () => dcs.Remove<string>(DataCacheName.ForwardLossRatioMap);

            // then...
            thenAction.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ClearOk()
        {
            // given...
            var dcs = new DataCacheService();
            var testItem = new TestItem { Name = "basilt", Address = "1850 kirkwall Crescent" };

            // when..
            dcs.Add<string, TestItem>(DataCacheName.boundedContextState, "TestKey", testItem);
            dcs.Add<string, TestItem>(DataCacheName.ForwardLossRatioMap, "TestKey", testItem);
            dcs.Count.Should().Be(2);
            dcs.Clear();

            // then...
            dcs.Count.Should().Be(0);
        }
    }


}
