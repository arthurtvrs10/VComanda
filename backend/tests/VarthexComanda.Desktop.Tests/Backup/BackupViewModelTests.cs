using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Tests.Backup;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Backup;

public class BackupViewModelTests
{
    private static (BackupViewModel viewModel, FakeBackupService backupService, FakeConfirmadorDeBackup confirmador) CriarViewModel(bool confirmar = true)
    {
        var backupService = new FakeBackupService();
        var registros = new FakeBackupRegistroRepository();
        registros.Registrar(new BackupRegistro
        {
            Id = 0,
            Arquivo = "varthex-comanda-2026-09-17-080000.db",
            Destino = "C:\\backups",
            CriadoEm = new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc),
            Status = StatusBackup.Sucesso,
            Checksum = "xyz",
            Mensagem = null
        });
        var confirmador = new FakeConfirmadorDeBackup { ProximaResposta = confirmar };

        var viewModel = new BackupViewModel(
            new CriarBackupManual(backupService),
            new RestaurarBackup(backupService),
            new ListarBackupsRecentes(registros),
            confirmador);

        return (viewModel, backupService, confirmador);
    }

    [Fact]
    public void Construtor_CarregaListaAutomaticamente()
    {
        var (viewModel, _, _) = CriarViewModel();

        Assert.Single(viewModel.Backups);
    }

    [Fact]
    public void CriarBackup_Sucesso_AtualizaListaEMensagem()
    {
        var (viewModel, backupService, _) = CriarViewModel();

        viewModel.CriarBackupCommand.Execute(null);

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
        Assert.Contains("sucesso", viewModel.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CriarBackup_Falha_MostraMensagemDeErro()
    {
        var backupService = new FakeBackupService { ProximaCriacaoFalha = true };
        var registros = new FakeBackupRegistroRepository();
        var viewModel = new BackupViewModel(
            new CriarBackupManual(backupService),
            new RestaurarBackup(backupService),
            new ListarBackupsRecentes(registros),
            new FakeConfirmadorDeBackup());

        viewModel.CriarBackupCommand.Execute(null);

        Assert.Contains("Falha simulada", viewModel.Mensagem);
    }

    [Fact]
    public void Restaurar_SemBackupSelecionado_ComandoDesabilitado()
    {
        var (viewModel, _, _) = CriarViewModel();

        Assert.False(viewModel.RestaurarCommand.CanExecute(null));
    }

    [Fact]
    public void Restaurar_ConfirmadorRecusa_NaoChamaRestaurarBackup()
    {
        var (viewModel, backupService, _) = CriarViewModel(confirmar: false);
        viewModel.BackupSelecionado = viewModel.Backups[0];

        viewModel.RestaurarCommand.Execute(null);

        Assert.Equal(0, backupService.ChamadasRestaurarPara); // nenhuma chamada de restauração foi feita
    }

    [Fact]
    public void Restaurar_ConfirmadorAceitaESucesso_DisparaSolicitouReinicio()
    {
        var (viewModel, backupService, _) = CriarViewModel(confirmar: true);
        viewModel.BackupSelecionado = viewModel.Backups[0];
        var reinicioSolicitado = false;
        viewModel.SolicitouReinicio += (_, _) => reinicioSolicitado = true;

        viewModel.RestaurarCommand.Execute(null);

        Assert.True(reinicioSolicitado);
        Assert.Equal(1, backupService.ChamadasRestaurarPara);
    }

    [Fact]
    public void Restaurar_ConfirmadorAceitaMasFalha_MostraMensagemSemDispararReinicio()
    {
        var backupService = new FakeBackupService { ProximaRestauracaoFalha = true };
        var registros = new FakeBackupRegistroRepository();
        registros.Registrar(new BackupRegistro
        {
            Id = 0,
            Arquivo = "a.db",
            Destino = "C:\\backups",
            CriadoEm = DateTime.UtcNow,
            Status = StatusBackup.Sucesso,
            Checksum = "x",
            Mensagem = null
        });
        var viewModel = new BackupViewModel(
            new CriarBackupManual(backupService),
            new RestaurarBackup(backupService),
            new ListarBackupsRecentes(registros),
            new FakeConfirmadorDeBackup { ProximaResposta = true });
        viewModel.BackupSelecionado = viewModel.Backups[0];
        var reinicioSolicitado = false;
        viewModel.SolicitouReinicio += (_, _) => reinicioSolicitado = true;

        viewModel.RestaurarCommand.Execute(null);

        Assert.False(reinicioSolicitado);
        Assert.Contains("Falha simulada", viewModel.Mensagem);
        Assert.Equal(1, backupService.ChamadasRestaurarPara);
    }
}
