using System;
using Xunit;
using FluentAssertions;
using NSubstitute;
using System.Data;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class DbConnectionExtensionTests
    {
        [Fact]
        public void SetConnectionStringOk()
        {
            // Arrange
            var mockDbConn = Substitute.For<IDbConnection>();

            // Act
            var dbConn = DbConnectionExtension.SetConnectionString(mockDbConn, "data=1234");

            // Assert
            dbConn.Should().BeSameAs(mockDbConn);
            dbConn.Received().ConnectionString = "data=1234";
        }

        [Fact]
        public void SetConnectionStringWithEmptyValue()
        {
            // Arrange
            var mockDbConn = Substitute.For<IDbConnection>();

            // Act
            var dbConn = DbConnectionExtension.SetConnectionString(mockDbConn, "");

            // Assert
            dbConn.Should().BeSameAs(mockDbConn);
            dbConn.Received().ConnectionString = "";
        }

        [Fact]
        public void SetConnectionStringWithBlankValue()
        {
            // Arrange
            var mockDbConn = Substitute.For<IDbConnection>();

            // Act
            var dbConn = DbConnectionExtension.SetConnectionString(mockDbConn, "   ");

            // Assert
            dbConn.Should().BeSameAs(mockDbConn);
            dbConn.Received().ConnectionString = "   ";
        }

        [Fact]
        public void SetConnectionStringWithNullValue()
        {
            // Arrange
            var mockDbConn = Substitute.For<IDbConnection>();

            // Act
            var dbConn = DbConnectionExtension.SetConnectionString(mockDbConn, null);

            // Assert
            dbConn.Should().BeSameAs(mockDbConn);
            dbConn.Received().ConnectionString = null;
        }

        [Fact]
        public void SetConnectionStringWithNullDbConnectionParameter()
        {
            // Arrange
            IDbConnection nullConn = null;

            // Act
            var act = () => DbConnectionExtension.SetConnectionString(nullConn, "data=1234");

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
    }
}
