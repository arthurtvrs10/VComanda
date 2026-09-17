using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class PesquisarProdutos
{
    private readonly IProdutoRepository _produtos;

    public PesquisarProdutos(IProdutoRepository produtos)
    {
        _produtos = produtos;
    }

    public IReadOnlyList<Produto> Executar(int? categoriaId, string? texto) => _produtos.Pesquisar(categoriaId, texto);
}
