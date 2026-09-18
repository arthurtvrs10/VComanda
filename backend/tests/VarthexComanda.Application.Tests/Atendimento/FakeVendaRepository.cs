using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Atendimento;

public class FakeVendaRepository : IVendaRepository
{
    private readonly List<VendaResumo> _vendas = new();
    private readonly List<ItemComanda> _itensPorComanda = new();

    public int ChamadasListarPorData { get; private set; }
    public bool LancarExcecao { get; set; }

    public void AdicionarVenda(Venda venda, int numeroComanda, IEnumerable<ItemComanda> itens)
    {
        _vendas.Add(new VendaResumo { Venda = venda, NumeroComanda = numeroComanda });
        _itensPorComanda.AddRange(itens);
    }

    public IReadOnlyList<VendaResumo> ListarPorData(DateTime inicioUtc, DateTime fimUtc)
    {
        if (LancarExcecao)
        {
            throw new InvalidOperationException("Falha simulada.");
        }

        ChamadasListarPorData++;
        return _vendas
            .Where(vr => vr.Venda.FinalizadaEm >= inicioUtc && vr.Venda.FinalizadaEm < fimUtc)
            .OrderBy(vr => vr.Venda.FinalizadaEm)
            .ToList();
    }

    public IReadOnlyList<ItemComanda>? BuscarItensDaVenda(int vendaId)
    {
        if (LancarExcecao)
        {
            throw new InvalidOperationException("Falha simulada.");
        }

        var vendaResumo = _vendas.FirstOrDefault(vr => vr.Venda.Id == vendaId);
        if (vendaResumo is null)
        {
            return null;
        }

        return _itensPorComanda.Where(i => i.ComandaId == vendaResumo.Venda.ComandaId).ToList();
    }
}
