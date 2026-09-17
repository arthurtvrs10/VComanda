using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Atendimento;

public class EfComandaRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfComandaRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public IReadOnlyList<Comanda> ListarAbertas()
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Comandas
            .Where(c => c.Status == StatusComanda.Aberta)
            .OrderBy(c => c.Numero)
            .ToList();
    }

    public ComandaComItens? BuscarComItens(int comandaId)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == comandaId);
        if (comanda is null)
        {
            return null;
        }

        var itens = contexto.ItensComanda
            .Where(i => i.ComandaId == comandaId)
            .OrderBy(i => i.Id)
            .ToList();
        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public Comanda AbrirComanda(int numero, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = new Comanda
        {
            Id = 0,
            Numero = numero,
            Status = StatusComanda.Aberta,
            AbertaEm = agora,
            FechadaEm = null,
            TotalCentavos = 0
        };
        contexto.Comandas.Add(comanda);
        try
        {
            contexto.SaveChanges();
        }
        catch (DbUpdateException)
        {
            throw new NumeroComandaOcupadoException();
        }
        return comanda;
    }
}
