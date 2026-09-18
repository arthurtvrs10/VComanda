namespace VarthexComanda.Application.Configuracao;

public class ObterConfiguracao
{
    private readonly IConfiguracaoRepository _configuracoes;

    public ObterConfiguracao(IConfiguracaoRepository configuracoes)
    {
        _configuracoes = configuracoes;
    }

    public ConfiguracaoEstabelecimento Executar()
    {
        var nome = _configuracoes.ObterValor("estabelecimento.nome") ?? string.Empty;

        var quantidadeTexto = _configuracoes.ObterValor("comandas.quantidade_maxima");
        int? quantidade = int.TryParse(quantidadeTexto, out var quantidadeValor) ? quantidadeValor : null;

        var pastaExterna = _configuracoes.ObterValor("backup.pasta_externa");
        if (string.IsNullOrEmpty(pastaExterna))
        {
            pastaExterna = null;
        }

        return new ConfiguracaoEstabelecimento
        {
            NomeEstabelecimento = nome,
            QuantidadeMaximaComandas = quantidade,
            PastaBackupExterna = pastaExterna
        };
    }
}
