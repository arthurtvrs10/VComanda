using VarthexComanda.Application.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class ResultadoTests
{
    [Fact]
    public void Ok_ExpoeValorESucessoVerdadeiro()
    {
        var resultado = Resultado<string>.Ok("valor");

        Assert.True(resultado.Sucesso);
        Assert.Equal("valor", resultado.Valor);
        Assert.Empty(resultado.Erros);
    }

    [Fact]
    public void Falha_ExpoeErrosESucessoFalso()
    {
        var resultado = Resultado<string>.Falha("Erro 1", "Erro 2");

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.Valor);
        Assert.Equal(new[] { "Erro 1", "Erro 2" }, resultado.Erros);
    }
}
