using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public interface IProdutoRepository
{
    Produto? BuscarPorId(int id);
    IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto);
    Produto Salvar(Produto produto);
}
