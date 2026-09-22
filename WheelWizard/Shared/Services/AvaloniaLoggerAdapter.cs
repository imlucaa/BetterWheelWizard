using Avalonia;
using Avalonia.Logging;
using Microsoft.Extensions.Logging;

namespace WheelWizard.Shared.Services;

/// <summary>
/// An adapter for the Avalonia logging system to use Microsoft.Extensions.Logging.
/// </summary>
public class AvaloniaLoggerAdapter(ILogger<AvaloniaObject> logger) : ILogSink
{
    // We let the logger handle the log level, so we don't need to set it here.
    public bool IsEnabled(LogEventLevel level, string area) => true;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Log(level, area, source, messageTemplate, []);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        // The layout writes update logs which are annoying on info level so we set them to verbose
        if (area == "Layout" && level == LogEventLevel.Information)
            level = LogEventLevel.Verbose;

        var logLevel = level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
        };

#pragma warning disable CA2254
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        logger.Log(logLevel, messageTemplate, propertyValues);
#pragma warning restore CA2254
    }
}
