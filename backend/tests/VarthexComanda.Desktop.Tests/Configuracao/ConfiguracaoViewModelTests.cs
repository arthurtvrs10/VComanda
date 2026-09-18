using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using VarthexComanda.Desktop.Configuracao;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Configuracao;

public class ConfiguracaoViewModelTests
{
    private static (ConfiguracaoViewModel ViewModel, FakeConfiguracaoRepository Repositorio) CriarViewModel()
    {
        var repositorio = new FakeConfiguracaoRepository();
        var viewModel = new ConfiguracaoViewModel(
            new ObterConfiguracao(repositorio),
            new SalvarConfiguracao(repositorio, new FakeClock()));
        return (viewModel, repositorio);
    }

    [Fact]
    public void Construtor_CarregaConfiguracaoAtual()
    {
        var repositorio = new FakeConfiguracaoRepository();
        repositorio.Definir("estabelecimento.nome", "Lanchonete do Zé", DateTime.UtcNow);
        repositorio.Definir("comandas.quantidade_maxima", "30", DateTime.UtcNow);
        var viewModel = new ConfiguracaoViewModel(
            new ObterConfiguracao(repositorio),
            new SalvarConfiguracao(repositorio, new FakeClock()));

        Assert.Equal("Lanchonete do Zé", viewModel.NomeEstabelecimento);
        Assert.Equal("30", viewModel.QuantidadeComandasTexto);
    }

    [Fact]
    public void Salvar_DadosValidos_MostraMensagemDeSucesso()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NomeEstabelecimento = "Lanchonete do Zé";
        viewModel.QuantidadeComandasTexto = "30";

        viewModel.SalvarCommand.Execute(null);

        Assert.Equal("Configurações salvas com sucesso.", viewModel.Mensagem);
    }

    [Fact]
    public void Salvar_NomeVazio_MostraErroDoCasoDeUso()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NomeEstabelecimento = "";

        viewModel.SalvarCommand.Execute(null);

        Assert.Equal("Informe o nome do estabelecimento.", viewModel.Mensagem);
    }

    [Fact]
    public void Salvar_QuantidadeTextoInvalido_MostraErroSemGravarNada()
    {
        var (viewModel, repositorio) = CriarViewModel();
        viewModel.NomeEstabelecimento = "Lanchonete do Zé";
        viewModel.QuantidadeComandasTexto = "abc";

        viewModel.SalvarCommand.Execute(null);

        Assert.Equal("Informe uma quantidade de comandas válida.", viewModel.Mensagem);
        Assert.Null(repositorio.ObterValor("estabelecimento.nome"));
    }

    [Fact]
    public void DefinirPastaBackupExterna_AtualizaPropriedade()
    {
        var (viewModel, _) = CriarViewModel();

        viewModel.DefinirPastaBackupExterna("D:\\backups");

        Assert.Equal("D:\\backups", viewModel.PastaBackupExterna);
    }
}
