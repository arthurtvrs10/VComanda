using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class ComandaComItens
{
    public required Comanda Comanda { get; init; }
    public required IReadOnlyList<ItemComanda> Itens { get; init; }
}
