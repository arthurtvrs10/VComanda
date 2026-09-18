using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class EncerrarComanda
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;

    public EncerrarComanda(IComandaRepository comandas, IClock relogio)
    {
        _comandas = comandas;
        _relogio = relogio;
    }

    public Resultado<Venda> Executar(int comandaId)
    {
        var detalhe = _comandas.BuscarComItens(comandaId);
        if (detalhe is null)
        {
            return Resultado<Venda>.Falha("Comanda não encontrada.");
        }
        if (detalhe.Itens.Count == 0)
        {
            return Resultado<Venda>.Falha("Adicione um item antes de encerrar.");
        }

        try
        {
            return Resultado<Venda>.Ok(_comandas.EncerrarComanda(comandaId, _relogio.UtcNow));
        }
        catch (ComandaNaoAbertaException ex)
        {
            return Resultado<Venda>.Falha(ex.Message);
        }
    }
}
