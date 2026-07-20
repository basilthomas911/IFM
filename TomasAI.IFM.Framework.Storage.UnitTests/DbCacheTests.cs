using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class DbCacheTests
    {
        [Fact]
        public void DbCacheCreateOk()
        {
            // Arrange & Act
            var dbCache = new DbCache();

            // Assert
            dbCache.Count(e => e.EntityTypeIdMap).Should().Be(0);
            dbCache.Count(e => e.EventTypeIdMap).Should().Be(0);
        }

        [Fact]
        public void DbCacheLoadOk()
        {
            // Arrange
            var dbCache = new DbCache();
            var cacheEntries = new Dictionary<string, long>
            {
                { "Test1", 1 },
                { "Test2", 2 },
                { "Test3", 3 }
            };

            // Act
            dbCache.Load(e => e.EntityTypeIdMap, cacheEntries);

            // Assert
            dbCache.Count(e => e.EntityTypeIdMap).Should().Be(3);
        }

        [Fact]
        public void DbCacheLoadWithNullCacheEntries()
        {
            // Arrange
            var dbCache = new DbCache();
            var cacheEntries = default(Dictionary<string, long>);

            // Act
            var act = () => dbCache.Load(e => e.EntityTypeIdMap, cacheEntries);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DbCacheLoadWithInvalidCacheEntries()
        {
            // Arrange
            var dbCache = new DbCache();
            var cacheEntries = new Dictionary<long, string>();

            // Act
            var act = () => dbCache.Load(e => e.EntityTypeIdMap, cacheEntries);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void DbCacheGetOk()
        {
            // Arrange
            var dbCache = new DbCache();
            var cacheEntries = new Dictionary<string, long>
            {
                { "Test1", 1 },
                { "Test2", 2 },
                { "Test3", 3 }
            };
            dbCache.Load(e => e.EntityTypeIdMap, cacheEntries);

            // Act & Assert
            dbCache.Count(e => e.EntityTypeIdMap).Should().Be(3);
            dbCache.Get<string, long>(e => e.EntityTypeIdMap, "Test1").Should().Be(1);
            dbCache.Get<string, long>(e => e.EntityTypeIdMap, "Test2").Should().Be(2);
            dbCache.Get<string, long>(e => e.EntityTypeIdMap, "Test3").Should().Be(3);
        }

        [Fact]
        public void DbCacheGetWithNullKey()
        {
            // Arrange
            var dbCache = new DbCache();
            var cacheEntries = new Dictionary<string, long>
            {
                { "Test1", 1 },
                { "Test2", 2 },
                { "Test3", 3 }
            };
            dbCache.Load(e => e.EntityTypeIdMap, cacheEntries);

            // Act
            var act = () => dbCache.Get<string, long>(e => e.EntityTypeIdMap, null);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void DbCacheGetWithInvalidCacheEntries()
        {
            // Arrange
            var dbCache = new DbCache();

            // Act
            var act = () => dbCache.Get<long, string>(e => e.EntityTypeIdMap, 1, () => "Test1");

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void DbCacheGetWithNoCacheEntries()
        {
            // Arrange
            var dbCache = new DbCache();

            // Act & Assert
            dbCache.Get(e => e.EntityTypeIdMap, "Test1", () => 1L).Should().Be(1);
            dbCache.Count(e => e.EntityTypeIdMap).Should().Be(1);
            dbCache.Get(e => e.EntityTypeIdMap, "Test2", () => 2L).Should().Be(2);
            dbCache.Count(e => e.EntityTypeIdMap).Should().Be(2);
            dbCache.Get(e => e.EntityTypeIdMap, "Test3", () => 3L).Should().Be(3);
            dbCache.Count(e => e.EntityTypeIdMap).Should().Be(3);
        }

        [Fact]
        public void DbCacheClearOk()
        {
            // Arrange
            var dbCache = new DbCache();
            var cacheEntries = new Dictionary<string, long>
            {
                { "Test1", 1 },
                { "Test2", 2 },
                { "Test3", 3 }
            };
            dbCache.Load(e => e.EntityTypeIdMap, cacheEntries);
            dbCache.Count(e => e.EntityTypeIdMap).Should().Be(3);

            // Act
            dbCache.Clear(e => e.EntityTypeIdMap);

            // Assert
            dbCache.Count(e => e.EntityTypeIdMap).Should().Be(0);
        }

        [Fact]
        public void DbCacheGetExistingKeyReturnsCachedValue()
        {
            // Arrange
            var dbCache = new DbCache();
            dbCache.Get(e => e.EntityTypeIdMap, "Key1", () => 100L);

            // Act
            var result = dbCache.Get(e => e.EntityTypeIdMap, "Key1", () => 999L);

            // Assert
            result.Should().Be(100);
        }
    }
}
