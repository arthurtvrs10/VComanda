using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class AbrirComanda
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;
    private readonly ObterConfiguracao _obterConfiguracao;

    public AbrirComanda(IComandaRepository comandas, IClock relogio, ObterConfiguracao obterConfiguracao)
    {
        _comandas = comandas;
        _relogio = relogio;
        _obterConfiguracao = obterConfiguracao;
    }

    public Resultado<Comanda> Executar(int numero)
    {
        if (numero <= 0)
        {
            return Resultado<Comanda>.Falha("Informe um número de comanda válido.");
        }

        var configuracao = _obterConfiguracao.Executar();
        if (configuracao.QuantidadeMaximaComandas is int maximo && numero > maximo)
        {
            return Resultado<Comanda>.Falha($"O número da comanda deve ser no máximo {maximo}.");
        }

        try
        {
            return Resultado<Comanda>.Ok(_comandas.AbrirComanda(numero, _relogio.UtcNow));
        }
        catch (NumeroComandaOcupadoException ex)
        {
            return Resultado<Comanda>.Falha(ex.Message);
        }
    }
}
