using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class ListarVendasPorDataTests
{
    private static Venda CriarVenda(DateTime finalizadaEmUtc) => new()
    {
        Id = 1,
        ComandaId = 1,
        Numero = 1,
        TotalCentavos = 1000,
        FinalizadaEm = finalizadaEmUtc,
        Status = StatusVenda.Concluida
    };

    [Fact]
    public void Executar_VendaAs23hBrasiliaDoDiaX_ApareceNoDiaLocalX()
    {
        var vendas = new FakeVendaRepository();
        // 23h de 18/09 em Brasília = 02h UTC de 19/09
        vendas.AdicionarVenda(CriarVenda(new DateTime(2026, 9, 19, 2, 0, 0)), 10, new List<ItemComanda>());
        var caso = new ListarVendasPorData(vendas);

        var resultado = caso.Executar(new DateTime(2026, 9, 18, 0, 0, 0));

        Assert.Single(resultado);
    }

    [Fact]
    public void Executar_VendaAMeiaNoiteLocalExata_CaiNoDiaCerto()
    {
        var vendas = new FakeVendaRepository();
        // meia-noite de 18/09 em Brasília = 03h UTC de 18/09
        vendas.AdicionarVenda(CriarVenda(new DateTime(2026, 9, 18, 3, 0, 0)), 10, new List<ItemComanda>());
        var caso = new ListarVendasPorData(vendas);

        var resultado = caso.Executar(new DateTime(2026, 9, 18, 0, 0, 0));

        Assert.Single(resultado);
    }

    [Fact]
    public void Executar_DiaSemVendas_RetornaListaVazia()
    {
        var vendas = new FakeVendaRepository();
        var caso = new ListarVendasPorData(vendas);

        var resultado = caso.Executar(new DateTime(2026, 9, 18, 0, 0, 0));

        Assert.Empty(resultado);
    }
}
