using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class FileStringReaderTests
{
    [Fact]
    public void CreateFileStringReaderOk()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var uri = new Uri(tempFile);
            var reader = new FileStringReader(uri);
            reader.Should().NotBeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void CreateFileStringReaderWithNullUri()
    {
        var act = () => { _ = new FileStringReader(null); };
            act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateFileStringReaderWithNonFileUri()
    {
        var act = () => { var uri = new Uri("https://example.com/data.csv");
            _ = new FileStringReader(uri); };
            act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ReadToEndAsyncOk()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "Hello World");
            var uri = new Uri(tempFile);
            var reader = new FileStringReader(uri);
            var content = await reader.ReadToEndAsync();
            "Hello World".Should().Be(content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadToEndAsyncWithEmptyFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var uri = new Uri(tempFile);
            var reader = new FileStringReader(uri);
            var content = await reader.ReadToEndAsync();
            "".Should().Be(content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadLinesAsyncOk()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "Line1\nLine2\nLine3");
            var uri = new Uri(tempFile);
            var reader = new FileStringReader(uri);
            var lines = new System.Collections.Generic.List<string>();
            await foreach (var line in reader.ReadLinesAsync())
            {
                lines.Add(line);
            }
            3.Should().Be(lines.Count);
            "Line1".Should().Be(lines[0]);
            "Line2".Should().Be(lines[1]);
            "Line3".Should().Be(lines[2]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadLinesAsyncWithEmptyFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var uri = new Uri(tempFile);
            var reader = new FileStringReader(uri);
            var lines = new System.Collections.Generic.List<string>();
            await foreach (var line in reader.ReadLinesAsync())
            {
                lines.Add(line);
            }
            0.Should().Be(lines.Count);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadToEndAsyncWithMultiLineContent()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var content = "First line\r\nSecond line\r\nThird line";
            await File.WriteAllTextAsync(tempFile, content);
            var uri = new Uri(tempFile);
            var reader = new FileStringReader(uri);
            var result = await reader.ReadToEndAsync();
            content.Should().Be(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadLinesAsyncWithSingleLine()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "SingleLine");
            var uri = new Uri(tempFile);
            var reader = new FileStringReader(uri);
            var lines = new System.Collections.Generic.List<string>();
            await foreach (var line in reader.ReadLinesAsync())
            {
                lines.Add(line);
            }
            1.Should().Be(lines.Count);
            "SingleLine".Should().Be(lines[0]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
