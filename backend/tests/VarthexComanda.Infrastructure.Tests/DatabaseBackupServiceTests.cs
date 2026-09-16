using VarthexComanda.Infrastructure.Storage;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class DatabaseBackupServiceTests
{
    [Fact]
    public void BackupIfExists_QuandoBancoNaoExiste_NaoCriaCopiaERetornaNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Combine(root, "varthex-comanda.db");
            var backupsDir = Path.Combine(root, "backups");
            Directory.CreateDirectory(backupsDir);

            var resultado = DatabaseBackupService.BackupIfExists(dbPath, backupsDir, DateTime.UtcNow);

            Assert.Null(resultado);
            Assert.Empty(Directory.GetFiles(backupsDir));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BackupIfExists_QuandoBancoExiste_CopiaComNomeCarimbadoNoHorario()
    {
        var root = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Combine(root, "varthex-comanda.db");
            File.WriteAllText(dbPath, "conteudo-fake-do-banco");
            var backupsDir = Path.Combine(root, "backups");
            Directory.CreateDirectory(backupsDir);
            var timestamp = new DateTime(2026, 9, 16, 14, 30, 0, DateTimeKind.Utc);

            var resultado = DatabaseBackupService.BackupIfExists(dbPath, backupsDir, timestamp);

            Assert.NotNull(resultado);
            Assert.True(File.Exists(resultado));
            Assert.Equal("varthex-comanda-2026-09-16-143000.db", Path.GetFileName(resultado));
            Assert.Equal("conteudo-fake-do-banco", File.ReadAllText(resultado));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
