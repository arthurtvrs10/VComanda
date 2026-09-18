using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class EncerramentoViewModelTests
{
    private static (EncerramentoViewModel viewModel, FakeComandaRepository comandas, int comandaId) CriarViewModel(bool comItem = true)
    {
        var relogio = new FakeClock();
        var comandas = new FakeComandaRepository();
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        if (comItem)
        {
            var produto = new Produto { Id = 1, CategoriaId = 1, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
            comandas.AdicionarItem(comanda.Id, produto, 2, relogio.UtcNow);
        }

        var viewModel = new EncerramentoViewModel(new EncerrarComanda(comandas, relogio), comandas);
        viewModel.Carregar(comanda.Id);
        return (viewModel, comandas, comanda.Id);
    }

    [Fact]
    public void Carregar_ComandaComItens_PreencheNumeroTotalEItens()
    {
        var (viewModel, _, _) = CriarViewModel();

        Assert.Equal(10, viewModel.NumeroComanda);
        Assert.Equal(1000, viewModel.TotalCentavos);
        Assert.Single(viewModel.Itens);
    }

    [Fact]
    public void ConfirmarEncerrar_SemCobrancaAprovada_ComandoDesabilitado()
    {
        var (viewModel, _, _) = CriarViewModel();

        Assert.False(viewModel.ConfirmarEncerrarCommand.CanExecute(null));
    }

    [Fact]
    public void ConfirmarEncerrar_CobrancaAprovada_FechaComandaEDisparaConcluidoTrue()
    {
        var (viewModel, comandas, comandaId) = CriarViewModel();
        bool? resultadoEvento = null;
        viewModel.Concluido += (_, sucesso) => resultadoEvento = sucesso;
        viewModel.CobrancaAprovada = true;

        viewModel.ConfirmarEncerrarCommand.Execute(null);

        Assert.True(resultadoEvento);
        Assert.Empty(comandas.ListarAbertas());
    }

    [Fact]
    public void ConfirmarEncerrar_ComandaVazia_MostraMensagemENaoDisparaConcluido()
    {
        var (viewModel, comandas, comandaId) = CriarViewModel(comItem: false);
        bool eventoDisparado = false;
        viewModel.Concluido += (_, __) => eventoDisparado = true;
        viewModel.CobrancaAprovada = true;

        viewModel.ConfirmarEncerrarCommand.Execute(null);

        Assert.False(eventoDisparado);
        Assert.Equal("Adicione um item antes de encerrar.", viewModel.Mensagem);
        Assert.Single(comandas.ListarAbertas());
    }

    [Fact]
    public void Voltar_DisparaConcluidoFalseSemAlterarComanda()
    {
        var (viewModel, comandas, comandaId) = CriarViewModel();
        bool? resultadoEvento = null;
        viewModel.Concluido += (_, sucesso) => resultadoEvento = sucesso;

        viewModel.VoltarCommand.Execute(null);

        Assert.False(resultadoEvento);
        Assert.Single(comandas.ListarAbertas());
    }
}
