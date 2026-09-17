using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public interface ICategoriaRepository
{
    Categoria? BuscarPorId(int id);
    IReadOnlyList<Categoria> ListarAtivas();
    bool ExisteNome(string nome, int? ignorarId = null);
    Categoria Salvar(Categoria categoria);
}
