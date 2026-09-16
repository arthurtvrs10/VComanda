using VarthexComanda.Infrastructure.Logging;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class LoggingConfiguratorTests
{
    [Fact]
    public void CreateLogger_EscreveArquivoDeLogNaPastaIndicada()
    {
        var logsDir = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(logsDir);
        try
        {
            var logger = LoggingConfigurator.CreateLogger(logsDir);

            logger.Information("mensagem de teste {Marcador}", "abc123");
            (logger as IDisposable)?.Dispose();

            var arquivos = Directory.GetFiles(logsDir, "varthex-comanda-*.log");
            Assert.Single(arquivos);
            Assert.Contains("abc123", File.ReadAllText(arquivos[0]));
        }
        finally
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }
}
