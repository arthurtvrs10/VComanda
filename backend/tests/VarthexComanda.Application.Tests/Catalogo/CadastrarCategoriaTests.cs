using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class CadastrarCategoriaTests
{
    [Fact]
    public void Executar_NomeValido_CriaCategoriaAtiva()
    {
        var caso = new CadastrarCategoria(new FakeCategoriaRepository(), new FakeClock());

        var resultado = caso.Executar("Bebidas");

        Assert.True(resultado.Sucesso);
        Assert.Equal("Bebidas", resultado.Valor!.Nome);
        Assert.True(resultado.Valor.Ativo);
        Assert.True(resultado.Valor.Id > 0);
    }

    [Fact]
    public void Executar_NomeVazioOuComEspacos_Falha()
    {
        var caso = new CadastrarCategoria(new FakeCategoriaRepository(), new FakeClock());

        var resultado = caso.Executar("   ");

        Assert.False(resultado.Sucesso);
        Assert.Contains("Informe o nome da categoria.", resultado.Erros);
    }

    [Fact]
    public void Executar_NomeDuplicado_Falha()
    {
        var repositorio = new FakeCategoriaRepository();
        var caso = new CadastrarCategoria(repositorio, new FakeClock());
        caso.Executar("Bebidas");

        var resultado = caso.Executar("BEBIDAS");

        Assert.False(resultado.Sucesso);
        Assert.Contains("Já existe uma categoria com esse nome.", resultado.Erros);
    }
}
