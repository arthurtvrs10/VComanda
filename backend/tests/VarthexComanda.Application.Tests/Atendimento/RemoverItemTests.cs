using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class RemoverItemTests
{
    [Fact]
    public void Executar_ItemExistente_RemoveERecalculaTotal()
    {
        var comandas = new FakeComandaRepository();
        var comanda = new AbrirComanda(comandas, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository())).Executar(10).Valor!;
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var produto = new CadastrarProduto(produtos, categorias, new FakeClock()).Executar("Refrigerante", categoria.Id, 500).Valor!;
        var item = new AdicionarItem(comandas, produtos, new FakeClock()).Executar(comanda.Id, produto.Id, 1).Valor!.Itens[0];
        var caso = new RemoverItem(comandas, new FakeClock());

        var resultado = caso.Executar(item.Id);

        Assert.True(resultado.Sucesso);
        Assert.Empty(resultado.Valor!.Itens);
        Assert.Equal(0, resultado.Valor.Comanda.TotalCentavos);
    }

    [Fact]
    public void Executar_ItemInexistente_Falha()
    {
        var caso = new RemoverItem(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(999);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Item não encontrado.", resultado.Erros);
    }
}
