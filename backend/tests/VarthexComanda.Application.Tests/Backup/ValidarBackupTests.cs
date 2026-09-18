using VarthexComanda.Application.Backup;
using Xunit;

namespace VarthexComanda.Application.Tests.Backup;

public class ValidarBackupTests
{
    [Fact]
    public void Executar_RepassaORelatorioDoBackupService()
    {
        var backupService = new FakeBackupService();
        var caso = new ValidarBackup(backupService);

        var relatorio = caso.Executar("C:\\qualquer.db");

        Assert.Equal(backupService.ProximoRelatorio, relatorio);
    }
}
