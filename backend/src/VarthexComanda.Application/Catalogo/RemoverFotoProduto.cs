using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class RemoverFotoProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly IFotoStorage _fotos;
    private readonly IClock _relogio;

    public RemoverFotoProduto(IProdutoRepository produtos, IFotoStorage fotos, IClock relogio)
    {
        _produtos = produtos;
        _fotos = fotos;
        _relogio = relogio;
    }

    public Resultado<Produto> Executar(int produtoId)
    {
        var produto = _produtos.BuscarPorId(produtoId);
        if (produto is null)
        {
            return Resultado<Produto>.Falha("Produto não encontrado.");
        }

        var arquivo = produto.FotoArquivo;
        if (string.IsNullOrEmpty(arquivo))
        {
            return Resultado<Produto>.Ok(produto);
        }

        produto.FotoArquivo = null;
        produto.AtualizadoEm = _relogio.UtcNow;
        _produtos.Salvar(produto);
        _fotos.Excluir(arquivo);

        return Resultado<Produto>.Ok(produto);
    }
}
