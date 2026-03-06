using Bridge;
using Xunit;

namespace Bridge.Tests;

public class MtSicsCommandParserTests
{
    [Theory]
    [InlineData("SI\r\n", "SI")]
    [InlineData("SI\r",   "SI")]
    [InlineData("SI",     "SI")]
    [InlineData("S\r\n",  "S")]
    [InlineData("T\r\n",  "T")]
    [InlineData("  si \r\n", "SI")]   // whitespace + lower-case
    [InlineData("\r\n",   "")]         // empty line
    [InlineData("",       "")]         // empty string
    public void Parse_ReturnsNormalisedCommand(string input, string expected)
    {
        var result = MtSicsCommandParser.Parse(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Parse_NullInput_ReturnsEmpty()
    {
        var result = MtSicsCommandParser.Parse(null!);
        Assert.Equal(string.Empty, result);
    }
}
