using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Tests.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Backup;

public class CriarBackupAutomaticoTests
{
    [Fact]
    public void Executar_JaExisteBackupHoje_NaoCriaNovoBackup()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = true };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock());

        caso.Executar();

        Assert.Equal(0, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_NaoExisteBackupHoje_CriaBackup()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock());

        caso.Executar();

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_Incondicional_CriaBackupMesmoSeJaExisteHoje()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = true };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock());

        caso.Executar(incondicional: true);

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_BackupServiceLancaExcecao_NaoPropagaExcecao()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService { LancarExcecaoAoCriar = true };
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock());

        var excecao = Record.Exception(() => caso.Executar());

        Assert.Null(excecao);
    }
}
