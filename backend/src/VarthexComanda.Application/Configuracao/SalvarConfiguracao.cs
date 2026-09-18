using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Catalogo;

namespace VarthexComanda.Application.Configuracao;

public class SalvarConfiguracao
{
    private readonly IConfiguracaoRepository _configuracoes;
    private readonly IClock _relogio;

    public SalvarConfiguracao(IConfiguracaoRepository configuracoes, IClock relogio)
    {
        _configuracoes = configuracoes;
        _relogio = relogio;
    }

    public Resultado<ConfiguracaoEstabelecimento> Executar(ConfiguracaoEstabelecimento configuracao)
    {
        if (string.IsNullOrWhiteSpace(configuracao.NomeEstabelecimento))
        {
            return Resultado<ConfiguracaoEstabelecimento>.Falha("Informe o nome do estabelecimento.");
        }

        if (configuracao.QuantidadeMaximaComandas is int quantidade && quantidade <= 0)
        {
            return Resultado<ConfiguracaoEstabelecimento>.Falha("A quantidade de comandas deve ser maior que zero.");
        }

        var agora = _relogio.UtcNow;
        _configuracoes.Definir("estabelecimento.nome", configuracao.NomeEstabelecimento, agora);
        _configuracoes.Definir("comandas.quantidade_maxima", configuracao.QuantidadeMaximaComandas?.ToString() ?? string.Empty, agora);
        _configuracoes.Definir("backup.pasta_externa", configuracao.PastaBackupExterna ?? string.Empty, agora);

        return Resultado<ConfiguracaoEstabelecimento>.Ok(configuracao);
    }
}
