using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class ResultadoTests
{
    [Fact]
    public void Ok_ExpoeValorESucessoVerdadeiro()
    {
        var resultado = Resultado<int>.Ok(42);

        Assert.True(resultado.Sucesso);
        Assert.Equal(42, resultado.Valor);
        Assert.Empty(resultado.Erros);
    }

    [Fact]
    public void Falha_ExpoeErrosESucessoFalso()
    {
        var resultado = Resultado<int>.Falha("Erro 1", "Erro 2");

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.Valor);
        Assert.Equal(new[] { "Erro 1", "Erro 2" }, resultado.Erros);
    }
}
