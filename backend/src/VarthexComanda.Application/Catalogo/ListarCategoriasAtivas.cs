using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class ListarCategoriasAtivas
{
    private readonly ICategoriaRepository _repositorio;

    public ListarCategoriasAtivas(ICategoriaRepository repositorio)
    {
        _repositorio = repositorio;
    }

    public IReadOnlyList<Categoria> Executar() => _repositorio.ListarAtivas();
}
