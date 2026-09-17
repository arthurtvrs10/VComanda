using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Catalogo;

namespace VarthexComanda.Application.Atendimento;

public class AlterarQuantidade
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;

    public AlterarQuantidade(IComandaRepository comandas, IClock relogio)
    {
        _comandas = comandas;
        _relogio = relogio;
    }

    public Resultado<ComandaComItens> Executar(int itemId, int quantidade)
    {
        if (quantidade <= 0)
        {
            return Resultado<ComandaComItens>.Falha("A quantidade deve ser maior que zero.");
        }

        try
        {
            return Resultado<ComandaComItens>.Ok(_comandas.AlterarQuantidade(itemId, quantidade, _relogio.UtcNow));
        }
        catch (ComandaNaoAbertaException ex)
        {
            return Resultado<ComandaComItens>.Falha(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Resultado<ComandaComItens>.Falha(ex.Message);
        }
    }
}
