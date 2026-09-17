using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class CadastrarProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly ICategoriaRepository _categorias;
    private readonly IClock _relogio;

    public CadastrarProduto(IProdutoRepository produtos, ICategoriaRepository categorias, IClock relogio)
    {
        _produtos = produtos;
        _categorias = categorias;
        _relogio = relogio;
    }

    public Resultado<Produto> Executar(string nome, int categoriaId, long precoCentavos)
    {
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

        var agora = _relogio.UtcNow;
        var produto = new Produto
        {
            Id = 0,
            CategoriaId = categoriaId,
            Nome = nomeNormalizado,
            PrecoCentavos = precoCentavos,
            Ativo = true,
            CriadoEm = agora,
            AtualizadoEm = agora
        };
        return Resultado<Produto>.Ok(_produtos.Salvar(produto));
    }
}
