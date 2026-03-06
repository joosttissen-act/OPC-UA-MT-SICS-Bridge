using Bridge;
using Xunit;

namespace Bridge.Tests;

public class MtSicsResponseFormatterTests
{
    [Fact]
    public void FormatWeight_StableReading_ContainsStatusS()
    {
        var result = MtSicsResponseFormatter.FormatWeight(1.234, stable: true);
        Assert.StartsWith("S S", result);
        Assert.EndsWith("\r\n", result);
    }

    [Fact]
    public void FormatWeight_UnstableReading_ContainsStatusD()
    {
        var result = MtSicsResponseFormatter.FormatWeight(1.235, stable: false);
        Assert.StartsWith("S D", result);
        Assert.EndsWith("\r\n", result);
    }

    [Fact]
    public void FormatWeight_ContainsWeightAndUnit()
    {
        var result = MtSicsResponseFormatter.FormatWeight(1.234, stable: true);
        Assert.Contains("1.234", result);
        Assert.Contains("kg", result);
    }

    [Fact]
    public void FormatWeight_ZeroWeight_FormatsCorrectly()
    {
        var result = MtSicsResponseFormatter.FormatWeight(0.0, stable: true);
        Assert.StartsWith("S S", result);
        Assert.Contains("0.000", result);
        Assert.Contains("kg", result);
        Assert.EndsWith("\r\n", result);
    }

    [Fact]
    public void FormatTareSuccess_ReturnsCorrectResponse()
    {
        var result = MtSicsResponseFormatter.FormatTareSuccess();
        Assert.Equal("T S\r\n", result);
    }

    [Fact]
    public void FormatExecutionError_WithSCommand_ReturnsSIResponse()
    {
        var result = MtSicsResponseFormatter.FormatExecutionError("S");
        Assert.Equal("S I\r\n", result);
    }

    [Fact]
    public void FormatExecutionError_WithTCommand_ReturnsTIResponse()
    {
        var result = MtSicsResponseFormatter.FormatExecutionError("T");
        Assert.Equal("T I\r\n", result);
    }

    [Fact]
    public void FormatUnknownCommand_ReturnsI()
    {
        var result = MtSicsResponseFormatter.FormatUnknownCommand();
        Assert.Equal("I\r\n", result);
    }
}
