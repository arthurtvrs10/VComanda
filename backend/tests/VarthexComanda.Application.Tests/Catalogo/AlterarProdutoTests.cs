using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class AlterarProdutoTests
{
    [Fact]
    public void Executar_DadosValidos_AtualizaCampos()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var produto = new CadastrarProduto(produtos, categorias, new FakeClock()).Executar("Refrigerante", categoria.Id, 500).Valor!;
        var caso = new AlterarProduto(produtos, categorias, new FakeClock());

        var resultado = caso.Executar(produto.Id, "Refrigerante Lata", categoria.Id, 600, ativo: false);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Refrigerante Lata", resultado.Valor!.Nome);
        Assert.Equal(600, resultado.Valor.PrecoCentavos);
        Assert.False(resultado.Valor.Ativo);
    }

    [Fact]
    public void Executar_ProdutoInexistente_Falha()
    {
        var caso = new AlterarProduto(new FakeProdutoRepository(), new FakeCategoriaRepository(), new FakeClock());

        var resultado = caso.Executar(999, "Qualquer", 1, 500, ativo: true);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Produto não encontrado.", resultado.Erros);
    }

    [Fact]
    public void Executar_CategoriaInativa_Falha()
    {
        var categorias = new FakeCategoriaRepository();
        var cadastrarCategoria = new CadastrarCategoria(categorias, new FakeClock());
        var ativa = cadastrarCategoria.Executar("Bebidas").Valor!;
        var inativa = cadastrarCategoria.Executar("Descontinuada").Valor!;
        new AlterarCategoria(categorias, new FakeClock()).Executar(inativa.Id, inativa.Nome, ativo: false);
        var produtos = new FakeProdutoRepository();
        var produto = new CadastrarProduto(produtos, categorias, new FakeClock()).Executar("Refrigerante", ativa.Id, 500).Valor!;
        var caso = new AlterarProduto(produtos, categorias, new FakeClock());

        var resultado = caso.Executar(produto.Id, "Refrigerante", inativa.Id, 500, ativo: true);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Selecione uma categoria ativa.", resultado.Erros);
    }
}
