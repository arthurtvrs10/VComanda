using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

/// <summary>
/// Repositório somente leitura. A escrita de uma venda acontece via
/// <see cref="IComandaRepository.EncerrarComanda"/>, que fecha a venda e o
/// status da comanda em uma única transação — não adicione métodos de escrita aqui.
/// </summary>
public interface IVendaRepository
{
    IReadOnlyList<VendaResumo> ListarPorData(DateTime inicioUtc, DateTime fimUtc);
    IReadOnlyList<ItemComanda>? BuscarItensDaVenda(int vendaId);
}
