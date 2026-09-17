using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class CancelarComandaTests
{
    [Fact]
    public void Executar_ComandaAberta_CancelaELiberaNumero()
    {
        var repositorio = new FakeComandaRepository();
        var abrir = new AbrirComanda(repositorio, new FakeClock());
        var comanda = abrir.Executar(10).Valor!;
        var caso = new CancelarComanda(repositorio, new FakeClock());

        var resultado = caso.Executar(comanda.Id);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusComanda.Cancelada, resultado.Valor!.Status);
        Assert.NotNull(resultado.Valor.FechadaEm);
        Assert.Empty(repositorio.ListarAbertas());

        var reabertura = abrir.Executar(10);
        Assert.True(reabertura.Sucesso);
    }

    [Fact]
    public void Executar_ComandaInexistente_Falha()
    {
        var caso = new CancelarComanda(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(999);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Comanda não encontrada.", resultado.Erros);
    }
}
