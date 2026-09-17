using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class DesativarProdutoTests
{
    [Fact]
    public void Executar_ProdutoExistente_DesativaSemExcluir()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var produto = new CadastrarProduto(produtos, categorias, new FakeClock()).Executar("Refrigerante", categoria.Id, 500).Valor!;
        var caso = new DesativarProduto(produtos, new FakeClock());

        var resultado = caso.Executar(produto.Id);

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.Valor!.Ativo);
        Assert.NotNull(produtos.BuscarPorId(produto.Id));
    }

    [Fact]
    public void Executar_ProdutoInexistente_Falha()
    {
        var caso = new DesativarProduto(new FakeProdutoRepository(), new FakeClock());

        var resultado = caso.Executar(999);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Produto não encontrado.", resultado.Erros);
    }
}
