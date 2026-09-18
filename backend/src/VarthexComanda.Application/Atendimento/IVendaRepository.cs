using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public interface IVendaRepository
{
    IReadOnlyList<VendaResumo> ListarPorData(DateTime inicioUtc, DateTime fimUtc);
    IReadOnlyList<ItemComanda>? BuscarItensDaVenda(int vendaId);
}
