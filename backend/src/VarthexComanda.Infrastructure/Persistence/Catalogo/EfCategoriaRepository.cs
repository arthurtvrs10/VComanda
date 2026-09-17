using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Catalogo;

public class EfCategoriaRepository : ICategoriaRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfCategoriaRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public Categoria? BuscarPorId(int id)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Categorias.SingleOrDefault(c => c.Id == id);
    }

    public IReadOnlyList<Categoria> ListarAtivas()
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Categorias.Where(c => c.Ativo).OrderBy(c => c.Nome).ToList();
    }

    public bool ExisteNome(string nome, int? ignorarId = null)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Categorias.Any(c => c.Nome == nome && c.Id != (ignorarId ?? 0));
    }

    public Categoria Salvar(Categoria categoria)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        if (categoria.Id == 0)
        {
            contexto.Categorias.Add(categoria);
        }
        else
        {
            contexto.Categorias.Update(categoria);
        }
        contexto.SaveChanges();
        return categoria;
    }
}
