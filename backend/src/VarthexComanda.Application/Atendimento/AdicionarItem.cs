using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Catalogo;

namespace VarthexComanda.Application.Atendimento;

public class AdicionarItem
{
    private readonly IComandaRepository _comandas;
    private readonly IProdutoRepository _produtos;
    private readonly IClock _relogio;

    public AdicionarItem(IComandaRepository comandas, IProdutoRepository produtos, IClock relogio)
    {
        _comandas = comandas;
        _produtos = produtos;
        _relogio = relogio;
    }

    public Resultado<ComandaComItens> Executar(int comandaId, int produtoId, int quantidade)
    {
        if (quantidade <= 0)
        {
            return Resultado<ComandaComItens>.Falha("A quantidade deve ser maior que zero.");
        }

        var produto = _produtos.BuscarPorId(produtoId);
        if (produto is null || !produto.Ativo)
        {
            return Resultado<ComandaComItens>.Falha("Produto indisponível.");
        }

        try
        {
            return Resultado<ComandaComItens>.Ok(_comandas.AdicionarItem(comandaId, produto, quantidade, _relogio.UtcNow));
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
