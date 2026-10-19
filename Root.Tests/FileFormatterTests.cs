using System;
using Root.Core.Utilities;
using Xunit;

namespace Root.Tests;

public class FileFormatterTests
{
    [Fact]
    public void FormatBytes_Zero_ReturnsZeroBytes()
    {
        var result = FileFormatter.FormatBytes(0);
        Assert.Equal("0 B", result);
    }

    [Fact]
    public void FormatBytes_BytesUnder1KB_ReturnsExactBytes()
    {
        var result = FileFormatter.FormatBytes(512);
        Assert.Equal("512 B", result);
    }

    [Fact]
    public void FormatBytes_Kilobytes_FormatsWithIndonesianDecimal()
    {
        var result = FileFormatter.FormatBytes(1536); // 1.5 KB
        Assert.Contains("1,50 KB", result);
    }

    [Fact]
    public void FormatBytes_Megabytes_FormatsProperly()
    {
        var result = FileFormatter.FormatBytes(1048576 * 10); // 10 MB
        Assert.Contains("10,00 MB", result);
    }

    [Fact]
    public void FormatBytes_Gigabytes_FormatsProperly()
    {
        long oneGb = 1024L * 1024L * 1024L;
        var result = FileFormatter.FormatBytes(oneGb * 2);
        Assert.Contains("2,00 GB", result);
    }

    [Fact]
    public void FormatDuration_Milliseconds_FormatsCorrectly()
    {
        var duration = TimeSpan.FromMilliseconds(450);
        var result = FileFormatter.FormatDuration(duration);
        Assert.Equal("450 ms", result);
    }

    [Fact]
    public void FormatDuration_Seconds_FormatsCorrectly()
    {
        var duration = TimeSpan.FromSeconds(15.4);
        var result = FileFormatter.FormatDuration(duration);
        Assert.Contains("15,4 dtk", result);
    }

    [Fact]
    public void FormatDuration_Minutes_FormatsCorrectly()
    {
        var duration = TimeSpan.FromMinutes(2.5);
        var result = FileFormatter.FormatDuration(duration);
        Assert.Equal("02:30 mnt", result);
    }

    [Fact]
    public void FormatNumber_FormatsWithThousandSeparator()
    {
        var result = FileFormatter.FormatNumber(1250000);
        Assert.Equal("1.250.000", result);
    }
}
