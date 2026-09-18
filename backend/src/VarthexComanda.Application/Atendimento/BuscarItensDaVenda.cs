using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class BuscarItensDaVenda
{
    private readonly IVendaRepository _vendas;

    public BuscarItensDaVenda(IVendaRepository vendas)
    {
        _vendas = vendas;
    }

    public IReadOnlyList<ItemComanda>? Executar(int vendaId) => _vendas.BuscarItensDaVenda(vendaId);
}
