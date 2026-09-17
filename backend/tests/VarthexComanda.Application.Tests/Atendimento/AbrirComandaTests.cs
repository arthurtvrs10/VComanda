using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class AbrirComandaTests
{
    [Fact]
    public void Executar_NumeroValido_AbreComanda()
    {
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(10);

        Assert.True(resultado.Sucesso);
        Assert.Equal(10, resultado.Valor!.Numero);
        Assert.Equal(StatusComanda.Aberta, resultado.Valor.Status);
    }

    [Fact]
    public void Executar_NumeroZeroOuNegativo_Falha()
    {
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(0);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Informe um número de comanda válido.", resultado.Erros);
    }

    [Fact]
    public void Executar_NumeroJaAberto_Falha()
    {
        var repositorio = new FakeComandaRepository();
        var caso = new AbrirComanda(repositorio, new FakeClock());
        caso.Executar(10);

        var resultado = caso.Executar(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Já existe uma comanda aberta com esse número.", resultado.Erros);
    }
}
