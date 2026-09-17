using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Catalogo;

namespace VarthexComanda.Application.Atendimento;

public class RemoverItem
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;

    public RemoverItem(IComandaRepository comandas, IClock relogio)
    {
        _comandas = comandas;
        _relogio = relogio;
    }

    public Resultado<ComandaComItens> Executar(int itemId)
    {
        try
        {
            return Resultado<ComandaComItens>.Ok(_comandas.RemoverItem(itemId, _relogio.UtcNow));
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
