using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class PesquisarProdutosTests
{
    [Fact]
    public void Executar_SemFiltro_RetornaTodos()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var cadastrarProduto = new CadastrarProduto(produtos, categorias, new FakeClock());
        cadastrarProduto.Executar("Refrigerante", categoria.Id, 500);
        cadastrarProduto.Executar("Suco", categoria.Id, 700);

        var resultado = new PesquisarProdutos(produtos).Executar(null, null);

        Assert.Equal(2, resultado.Count);
    }
}
