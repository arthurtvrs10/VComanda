using Serilog;

namespace VarthexComanda.Infrastructure.Logging;

public static class LoggingConfigurator
{
    public static ILogger CreateLogger(string logsDirectory)
    {
        return new LoggerConfiguration()
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "varthex-comanda-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
