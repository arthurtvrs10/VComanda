using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Desktop.Catalogo;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Catalogo;

public class ProdutosViewModelTests
{
    private static ProdutosViewModel CriarViewModel(FakeCategoriaRepository categorias, FakeProdutoRepository produtos)
    {
        var relogio = new FakeClock();
        return new ProdutosViewModel(
            new ListarCategoriasAtivas(categorias),
            new CadastrarCategoria(categorias, relogio),
            new PesquisarProdutos(produtos),
            new CadastrarProduto(produtos, categorias, relogio),
            new AlterarProduto(produtos, categorias, relogio),
            new DesativarProduto(produtos, relogio));
    }

    [Fact]
    public void PerderSelecao_LimpaFormulario_NaoDuplicaAoSalvarDeNovo()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var viewModel = CriarViewModel(categorias, produtos);
        viewModel.CategoriaProduto = categoria;
        viewModel.NomeProduto = "Refrigerante";
        viewModel.PrecoProdutoReais = "5,00";
        viewModel.SalvarCommand.Execute(null);

        viewModel.ProdutoSelecionado = viewModel.Produtos[0];
        viewModel.ProdutoSelecionado = null;
        viewModel.SalvarCommand.Execute(null);

        Assert.Single(produtos.Pesquisar(null, null));
    }

    [Fact]
    public void PrecoComPonto_NaoEhInterpretadoComoMilhar()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var viewModel = CriarViewModel(categorias, produtos);
        viewModel.CategoriaProduto = categoria;
        viewModel.NomeProduto = "Refrigerante";
        viewModel.PrecoProdutoReais = "10.50";

        viewModel.SalvarCommand.Execute(null);

        Assert.Empty(produtos.Pesquisar(null, null));
        Assert.Equal("Informe um preço válido.", viewModel.Mensagem);
    }
}
