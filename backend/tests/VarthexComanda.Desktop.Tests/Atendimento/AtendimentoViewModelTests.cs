using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Desktop.Atendimento;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class AtendimentoViewModelTests
{
    private static (AtendimentoViewModel viewModel, FakeProdutoRepository produtos) CriarViewModel()
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500);
        var comandas = new FakeComandaRepository();

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio),
            new AdicionarItem(comandas, produtos, relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(produtos));

        return (viewModel, produtos);
    }

    [Fact]
    public void Abrir_NumeroValido_CriaComandaEMostraNaGrade()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "10";

        viewModel.AbrirCommand.Execute(null);

        Assert.Single(viewModel.ComandasAbertas);
        Assert.NotNull(viewModel.ComandaAtual);
        Assert.Equal(10, viewModel.ComandaAtual!.Numero);
    }

    [Fact]
    public void Abrir_MesmoNumeroDuasVezes_SegundaFalhaSemDuplicarNaGrade()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        Assert.Equal("Já existe uma comanda aberta com esse número.", viewModel.Mensagem);
        Assert.Single(viewModel.ComandasAbertas);
    }

    [Fact]
    public void AdicionarProdutoDuasVezes_IncrementaQuantidadeEmVezDeDuplicar()
    {
        var (viewModel, produtos) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];

        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        Assert.Single(viewModel.Itens);
        Assert.Equal(2, viewModel.Itens[0].Quantidade);
        Assert.Equal(1000, viewModel.ComandaAtual!.TotalCentavos);
    }

    [Fact]
    public void DiminuirQuantidadeAteZero_RemoveItem()
    {
        var (viewModel, produtos) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        viewModel.DiminuirQuantidadeCommand.Execute(viewModel.Itens[0]);

        Assert.Empty(viewModel.Itens);
        Assert.Equal(0, viewModel.ComandaAtual!.TotalCentavos);
    }

    [Fact]
    public void CancelarComandaAtual_LiberaNumeroNaGrade()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        viewModel.CancelarComandaAtualCommand.Execute(null);

        Assert.Empty(viewModel.ComandasAbertas);
        Assert.Null(viewModel.ComandaAtual);
    }
}
