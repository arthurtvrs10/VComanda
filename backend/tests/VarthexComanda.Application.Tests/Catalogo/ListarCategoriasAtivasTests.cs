using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class ListarCategoriasAtivasTests
{
    [Fact]
    public void Executar_RetornaApenasAtivasOrdenadasPorNome()
    {
        var repositorio = new FakeCategoriaRepository();
        var cadastrar = new CadastrarCategoria(repositorio, new FakeClock());
        var bebidas = cadastrar.Executar("Bebidas").Valor!;
        cadastrar.Executar("Sobremesas");
        new AlterarCategoria(repositorio, new FakeClock()).Executar(bebidas.Id, bebidas.Nome, ativo: false);

        var ativas = new ListarCategoriasAtivas(repositorio).Executar();

        Assert.Single(ativas);
        Assert.Equal("Sobremesas", ativas[0].Nome);
    }
}
