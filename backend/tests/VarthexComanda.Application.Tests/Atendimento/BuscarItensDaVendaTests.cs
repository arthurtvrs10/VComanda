using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class BuscarItensDaVendaTests
{
    [Fact]
    public void Executar_VendaExistente_RetornaItensDaComandaOriginal()
    {
        var vendas = new FakeVendaRepository();
        var venda = new Venda { Id = 1, ComandaId = 5, Numero = 1, TotalCentavos = 500, FinalizadaEm = DateTime.UtcNow, Status = StatusVenda.Concluida };
        var item = new ItemComanda { Id = 1, ComandaId = 5, ProdutoId = 1, NomeProduto = "Refrigerante", PrecoUnitarioCentavos = 500, Quantidade = 1, SubtotalCentavos = 500, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow };
        vendas.AdicionarVenda(venda, 10, new[] { item });
        var caso = new BuscarItensDaVenda(vendas);

        var itens = caso.Executar(venda.Id);

        Assert.NotNull(itens);
        Assert.Single(itens!);
    }

    [Fact]
    public void Executar_VendaInexistente_RetornaNull()
    {
        var vendas = new FakeVendaRepository();
        var caso = new BuscarItensDaVenda(vendas);

        var itens = caso.Executar(999);

        Assert.Null(itens);
    }
}
