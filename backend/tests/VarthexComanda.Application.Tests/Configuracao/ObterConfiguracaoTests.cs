using VarthexComanda.Application.Configuracao;
using Xunit;

namespace VarthexComanda.Application.Tests.Configuracao;

public class ObterConfiguracaoTests
{
    [Fact]
    public void Executar_NadaConfigurado_RetornaValoresPadrao()
    {
        var repositorio = new FakeConfiguracaoRepository();
        var caso = new ObterConfiguracao(repositorio);

        var configuracao = caso.Executar();

        Assert.Equal(string.Empty, configuracao.NomeEstabelecimento);
        Assert.Null(configuracao.QuantidadeMaximaComandas);
        Assert.Null(configuracao.PastaBackupExterna);
    }

    [Fact]
    public void Executar_TudoConfigurado_RetornaValoresCorretos()
    {
        var repositorio = new FakeConfiguracaoRepository();
        repositorio.Definir("estabelecimento.nome", "Lanchonete do Zé", DateTime.UtcNow);
        repositorio.Definir("comandas.quantidade_maxima", "30", DateTime.UtcNow);
        repositorio.Definir("backup.pasta_externa", "D:\\backups", DateTime.UtcNow);
        var caso = new ObterConfiguracao(repositorio);

        var configuracao = caso.Executar();

        Assert.Equal("Lanchonete do Zé", configuracao.NomeEstabelecimento);
        Assert.Equal(30, configuracao.QuantidadeMaximaComandas);
        Assert.Equal("D:\\backups", configuracao.PastaBackupExterna);
    }

    [Fact]
    public void Executar_QuantidadeComValorInvalido_TratadoComoNulo()
    {
        var repositorio = new FakeConfiguracaoRepository();
        repositorio.Definir("comandas.quantidade_maxima", "abc", DateTime.UtcNow);
        var caso = new ObterConfiguracao(repositorio);

        var configuracao = caso.Executar();

        Assert.Null(configuracao.QuantidadeMaximaComandas);
    }
}
