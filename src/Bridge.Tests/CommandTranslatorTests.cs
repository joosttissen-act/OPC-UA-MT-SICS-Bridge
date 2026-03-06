using Bridge;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Bridge.Tests;

public class CommandTranslatorTests
{
    private readonly Mock<IOpcUaScaleClient> _mockClient;
    private readonly CommandTranslator _translator;

    public CommandTranslatorTests()
    {
        _mockClient = new Mock<IOpcUaScaleClient>();
        _translator = new CommandTranslator(_mockClient.Object, NullLogger<CommandTranslator>.Instance);
    }

    [Fact]
    public async Task HandleCommand_SI_ReturnsStableWeight()
    {
        _mockClient.Setup(c => c.ReadWeightAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1.234);
        _mockClient.Setup(c => c.IsStableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _translator.HandleCommandAsync("SI");

        Assert.StartsWith("S S", result);
        Assert.Contains("1.234", result);
        Assert.EndsWith("\r\n", result);
    }

    [Fact]
    public async Task HandleCommand_S_ReturnsDynamicWeight()
    {
        _mockClient.Setup(c => c.ReadWeightAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1.235);

        var result = await _translator.HandleCommandAsync("S");

        Assert.StartsWith("S D", result);
        Assert.Contains("1.235", result);
        Assert.EndsWith("\r\n", result);
    }

    [Fact]
    public async Task HandleCommand_T_ReturnsTareSuccess()
    {
        _mockClient.Setup(c => c.TareAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _translator.HandleCommandAsync("T");

        Assert.Equal("T S\r\n", result);
    }

    [Fact]
    public async Task HandleCommand_Unknown_ReturnsI()
    {
        var result = await _translator.HandleCommandAsync("UNKNOWN");

        Assert.Equal("I\r\n", result);
    }

    [Fact]
    public async Task HandleCommand_SI_WhenOpcUaFails_ReturnsSI()
    {
        _mockClient
            .Setup(c => c.ReadWeightAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OPC UA unavailable"));

        var result = await _translator.HandleCommandAsync("SI");

        Assert.Equal("S I\r\n", result);
    }

    [Fact]
    public async Task HandleCommand_T_WhenOpcUaFails_ReturnsTI()
    {
        _mockClient
            .Setup(c => c.TareAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OPC UA unavailable"));

        var result = await _translator.HandleCommandAsync("T");

        Assert.Equal("T I\r\n", result);
    }

    [Fact]
    public async Task HandleCommand_S_WhenOpcUaFails_ReturnsSI()
    {
        _mockClient
            .Setup(c => c.ReadWeightAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OPC UA unavailable"));

        var result = await _translator.HandleCommandAsync("S");

        Assert.Equal("S I\r\n", result);
    }
}
