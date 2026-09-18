using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Catalogo;
using Xunit;

namespace VarthexComanda.Application.Tests.Configuracao;

public class SalvarConfiguracaoTests
{
    [Fact]
    public void Executar_NomeVazio_Falha()
    {
        var repositorio = new FakeConfiguracaoRepository();
        var caso = new SalvarConfiguracao(repositorio, new FakeClock());

        var resultado = caso.Executar(new ConfiguracaoEstabelecimento
        {
            NomeEstabelecimento = "",
            QuantidadeMaximaComandas = null,
            PastaBackupExterna = null
        });

        Assert.False(resultado.Sucesso);
        Assert.Contains("Informe o nome do estabelecimento.", resultado.Erros);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Executar_QuantidadeZeroOuNegativa_Falha(int quantidade)
    {
        var repositorio = new FakeConfiguracaoRepository();
        var caso = new SalvarConfiguracao(repositorio, new FakeClock());

        var resultado = caso.Executar(new ConfiguracaoEstabelecimento
        {
            NomeEstabelecimento = "Lanchonete",
            QuantidadeMaximaComandas = quantidade,
            PastaBackupExterna = null
        });

        Assert.False(resultado.Sucesso);
        Assert.Contains("A quantidade de comandas deve ser maior que zero.", resultado.Erros);
    }

    [Fact]
    public void Executar_DadosValidos_GravaAsTresChavesERetornaSucesso()
    {
        var repositorio = new FakeConfiguracaoRepository();
        var caso = new SalvarConfiguracao(repositorio, new FakeClock());

        var resultado = caso.Executar(new ConfiguracaoEstabelecimento
        {
            NomeEstabelecimento = "Lanchonete do Zé",
            QuantidadeMaximaComandas = 30,
            PastaBackupExterna = "D:\\backups"
        });

        Assert.True(resultado.Sucesso);
        Assert.Equal("Lanchonete do Zé", repositorio.ObterValor("estabelecimento.nome"));
        Assert.Equal("30", repositorio.ObterValor("comandas.quantidade_maxima"));
        Assert.Equal("D:\\backups", repositorio.ObterValor("backup.pasta_externa"));
    }
}
