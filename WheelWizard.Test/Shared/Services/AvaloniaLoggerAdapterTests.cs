using Avalonia;
using Avalonia.Logging;
using Microsoft.Extensions.Logging;
using WheelWizard.Shared.Services;

namespace WheelWizard.Test.Shared.Services;

public class AvaloniaLoggerAdapterTests
{
    public static TheoryData<LogEventLevel, LogLevel> LogLevels = new()
    {
        { LogEventLevel.Verbose, LogLevel.Trace },
        { LogEventLevel.Debug, LogLevel.Debug },
        { LogEventLevel.Information, LogLevel.Information },
        { LogEventLevel.Warning, LogLevel.Warning },
        { LogEventLevel.Error, LogLevel.Error },
        { LogEventLevel.Fatal, LogLevel.Critical },
    };

    [Theory(DisplayName = "Log adapter message, should convert to logger message")]
    [MemberData(nameof(LogLevels))]
    public void LogAdapterMessage_ShouldConvertToLoggerMessage(LogEventLevel level, LogLevel expectedLevel)
    {
        // Arrange
        var logger = Substitute.For<ILogger<AvaloniaObject>>();
        var logAdapter = new AvaloniaLoggerAdapter(logger);

        var args = new object[] { "testValue" };

        // Act
        logAdapter.Log(level, "TestArea", null, "Test message {Arg}", args);

        // Assert
        logger.Received(1).Log(expectedLevel, "Test message {Arg}", args);
    }

    [Fact(DisplayName = "Log adapter without args, should log message without args")]
    public void LogAdapterWithoutArgs_ShouldLogMessageWithoutArgs()
    {
        // Arrange
        var logger = Substitute.For<ILogger<AvaloniaObject>>();
        var logAdapter = new AvaloniaLoggerAdapter(logger);

        // Act
        logAdapter.Log(LogEventLevel.Information, "TestArea", null, "Test message");

        // Assert
        logger.Received(1).Log(LogLevel.Information, "Test message");
    }

    [Fact(DisplayName = "Log adapter with layout area, should not log")]
    public void LogAdapterWithLayoutArea_ShouldNotLog()
    {
        // Arrange
        var logger = Substitute.For<ILogger<AvaloniaObject>>();
        var logAdapter = new AvaloniaLoggerAdapter(logger);

        // Act
        logAdapter.Log(LogEventLevel.Information, "Layout", null, "Test message");

        // Assert
#pragma warning disable CA2254
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        logger.DidNotReceive().Log(LogLevel.Information, "Test message");
#pragma warning restore CA2254
    }

    [Fact(DisplayName = "Log adapter is enabled, should be true")]
    public void LogAdapterIsEnabled_ShouldBeTrue()
    {
        // Arrange
        var logger = Substitute.For<ILogger<AvaloniaObject>>();
        var logAdapter = new AvaloniaLoggerAdapter(logger);

        // Act
        var result = logAdapter.IsEnabled(LogEventLevel.Information, "TestArea");

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "Invalid log event level log, should throw exception")]
    public void InvalidLogEventLevelLog_ShouldThrowException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<AvaloniaObject>>();
        var logAdapter = new AvaloniaLoggerAdapter(logger);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => logAdapter.Log((LogEventLevel)999, "TestArea", null, "Test message"));
    }
}
