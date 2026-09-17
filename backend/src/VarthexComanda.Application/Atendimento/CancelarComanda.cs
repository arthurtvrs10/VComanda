using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class CancelarComanda
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;

    public CancelarComanda(IComandaRepository comandas, IClock relogio)
    {
        _comandas = comandas;
        _relogio = relogio;
    }

    public Resultado<Comanda> Executar(int comandaId)
    {
        try
        {
            return Resultado<Comanda>.Ok(_comandas.CancelarComanda(comandaId, _relogio.UtcNow));
        }
        catch (ComandaNaoAbertaException ex)
        {
            return Resultado<Comanda>.Falha(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Resultado<Comanda>.Falha(ex.Message);
        }
    }
}
