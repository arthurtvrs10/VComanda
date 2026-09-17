using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class CadastrarProdutoTests
{
    private static (FakeCategoriaRepository categorias, int ativaId, int inativaId) PrepararCategorias()
    {
        var categorias = new FakeCategoriaRepository();
        var cadastrar = new CadastrarCategoria(categorias, new FakeClock());
        var ativa = cadastrar.Executar("Bebidas").Valor!;
        var inativa = cadastrar.Executar("Descontinuada").Valor!;
        new AlterarCategoria(categorias, new FakeClock()).Executar(inativa.Id, inativa.Nome, ativo: false);
        return (categorias, ativa.Id, inativa.Id);
    }

    [Fact]
    public void Executar_DadosValidos_CriaProdutoAtivo()
    {
        var (categorias, ativaId, _) = PrepararCategorias();
        var caso = new CadastrarProduto(new FakeProdutoRepository(), categorias, new FakeClock());

        var resultado = caso.Executar("Refrigerante", ativaId, 500);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Refrigerante", resultado.Valor!.Nome);
        Assert.True(resultado.Valor.Ativo);
        Assert.Equal(500, resultado.Valor.PrecoCentavos);
    }

    [Fact]
    public void Executar_NomeVazio_Falha()
    {
        var (categorias, ativaId, _) = PrepararCategorias();
        var caso = new CadastrarProduto(new FakeProdutoRepository(), categorias, new FakeClock());

        var resultado = caso.Executar("   ", ativaId, 500);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Informe o nome do produto.", resultado.Erros);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Executar_PrecoZeroOuNegativo_Falha(long preco)
    {
        var (categorias, ativaId, _) = PrepararCategorias();
        var caso = new CadastrarProduto(new FakeProdutoRepository(), categorias, new FakeClock());

        var resultado = caso.Executar("Refrigerante", ativaId, preco);

        Assert.False(resultado.Sucesso);
        Assert.Contains("O preço deve ser maior que zero.", resultado.Erros);
    }

    [Fact]
    public void Executar_CategoriaInativa_Falha()
    {
        var (categorias, _, inativaId) = PrepararCategorias();
        var caso = new CadastrarProduto(new FakeProdutoRepository(), categorias, new FakeClock());

        var resultado = caso.Executar("Refrigerante", inativaId, 500);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Selecione uma categoria ativa.", resultado.Erros);
    }

    [Fact]
    public void Executar_CategoriaInexistente_Falha()
    {
        var caso = new CadastrarProduto(new FakeProdutoRepository(), new FakeCategoriaRepository(), new FakeClock());

        var resultado = caso.Executar("Refrigerante", 999, 500);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Selecione uma categoria ativa.", resultado.Erros);
    }
}
