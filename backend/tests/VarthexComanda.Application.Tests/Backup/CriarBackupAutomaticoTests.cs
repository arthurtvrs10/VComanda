using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using Xunit;

namespace VarthexComanda.Application.Tests.Backup;

public class CriarBackupAutomaticoTests
{
    [Fact]
    public void Executar_JaExisteBackupHoje_NaoCriaNovoBackup()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = true };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()));

        caso.Executar();

        Assert.Equal(0, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_NaoExisteBackupHoje_CriaBackup()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()));

        caso.Executar();

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_Incondicional_CriaBackupMesmoSeJaExisteHoje()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = true };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()));

        caso.Executar(incondicional: true);

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_BackupServiceLancaExcecao_NaoPropagaExcecao()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService { LancarExcecaoAoCriar = true };
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()));

        var excecao = Record.Exception(() => caso.Executar());

        Assert.Null(excecao);
    }

    [Fact]
    public void Executar_IncondicionalComPastaExternaConfigurada_TentaCopiaExterna()
    {
        var registros = new FakeBackupRegistroRepository();
        var backupService = new FakeBackupService();
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("backup.pasta_externa", "D:\\backups", DateTime.UtcNow);
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(configuracoes));

        caso.Executar(incondicional: true);

        Assert.Equal(1, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_IncondicionalSemPastaExternaConfigurada_NaoTentaCopiaExterna()
    {
        var registros = new FakeBackupRegistroRepository();
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()));

        caso.Executar(incondicional: true);

        Assert.Equal(0, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_AberturaDoDiaComPastaExternaConfigurada_NuncaTentaCopiaExterna()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService();
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("backup.pasta_externa", "D:\\backups", DateTime.UtcNow);
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(configuracoes));

        caso.Executar();

        Assert.Equal(0, backupService.ChamadasCriarBackupExterno);
    }
}
