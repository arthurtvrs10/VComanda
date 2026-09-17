using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class AlterarCategoriaTests
{
    [Fact]
    public void Executar_CategoriaExistente_AtualizaNomeEAtivo()
    {
        var repositorio = new FakeCategoriaRepository();
        var criada = new CadastrarCategoria(repositorio, new FakeClock()).Executar("Bebidas").Valor!;
        var caso = new AlterarCategoria(repositorio, new FakeClock());

        var resultado = caso.Executar(criada.Id, "Bebidas Geladas", ativo: false);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Bebidas Geladas", resultado.Valor!.Nome);
        Assert.False(resultado.Valor.Ativo);
    }

    [Fact]
    public void Executar_CategoriaInexistente_Falha()
    {
        var caso = new AlterarCategoria(new FakeCategoriaRepository(), new FakeClock());

        var resultado = caso.Executar(999, "Qualquer", ativo: true);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Categoria não encontrada.", resultado.Erros);
    }

    [Fact]
    public void Executar_NomeDuplicadoComOutraCategoria_Falha()
    {
        var repositorio = new FakeCategoriaRepository();
        var cadastrar = new CadastrarCategoria(repositorio, new FakeClock());
        cadastrar.Executar("Bebidas");
        var sobremesas = cadastrar.Executar("Sobremesas").Valor!;
        var caso = new AlterarCategoria(repositorio, new FakeClock());

        var resultado = caso.Executar(sobremesas.Id, "BEBIDAS", ativo: true);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Já existe uma categoria com esse nome.", resultado.Erros);
    }
}
