using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class AlterarProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly ICategoriaRepository _categorias;
    private readonly IClock _relogio;

    public AlterarProduto(IProdutoRepository produtos, ICategoriaRepository categorias, IClock relogio)
    {
        _produtos = produtos;
        _categorias = categorias;
        _relogio = relogio;
    }

    public Resultado<Produto> Executar(int id, string nome, int categoriaId, long precoCentavos, bool ativo)
    {
        var produto = _produtos.BuscarPorId(id);
        if (produto is null)
        {
            return Resultado<Produto>.Falha("Produto não encontrado.");
        }

        var nomeNormalizado = (nome ?? string.Empty).Trim();
        if (nomeNormalizado.Length == 0)
        {
            return Resultado<Produto>.Falha("Informe o nome do produto.");
        }
        if (precoCentavos <= 0)
        {
            return Resultado<Produto>.Falha("O preço deve ser maior que zero.");
        }
        var categoria = _categorias.BuscarPorId(categoriaId);
        if (categoria is null || !categoria.Ativo)
        {
            return Resultado<Produto>.Falha("Selecione uma categoria ativa.");
        }

        produto.Nome = nomeNormalizado;
        produto.CategoriaId = categoriaId;
        produto.PrecoCentavos = precoCentavos;
        produto.Ativo = ativo;
        produto.AtualizadoEm = _relogio.UtcNow;
        return Resultado<Produto>.Ok(_produtos.Salvar(produto));
    }
}
