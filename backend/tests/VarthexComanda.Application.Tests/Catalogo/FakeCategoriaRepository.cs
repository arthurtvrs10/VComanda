using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Catalogo;

public class FakeCategoriaRepository : ICategoriaRepository
{
    private readonly List<Categoria> _categorias = new();
    private int _proximoId = 1;

    public Categoria? BuscarPorId(int id) => _categorias.FirstOrDefault(c => c.Id == id);

    public IReadOnlyList<Categoria> ListarAtivas() =>
        _categorias.Where(c => c.Ativo).OrderBy(c => c.Nome).ToList();

    public bool ExisteNome(string nome, int? ignorarId = null) =>
        _categorias.Any(c => c.Id != (ignorarId ?? 0) && string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase));

    public Categoria Salvar(Categoria categoria)
    {
        if (categoria.Id == 0)
        {
            categoria.Id = _proximoId++;
            _categorias.Add(categoria);
        }
        return categoria;
    }
}
