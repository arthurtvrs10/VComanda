using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class DesativarProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly IClock _relogio;

    public DesativarProduto(IProdutoRepository produtos, IClock relogio)
    {
        _produtos = produtos;
        _relogio = relogio;
    }

    public Resultado<Produto> Executar(int id)
    {
        var produto = _produtos.BuscarPorId(id);
        if (produto is null)
        {
            return Resultado<Produto>.Falha("Produto não encontrado.");
        }

        produto.Ativo = false;
        produto.AtualizadoEm = _relogio.UtcNow;
        return Resultado<Produto>.Ok(_produtos.Salvar(produto));
    }
}
