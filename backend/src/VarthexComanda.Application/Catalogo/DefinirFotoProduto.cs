using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class DefinirFotoProduto
{
    private readonly IProdutoRepository _produtos;
    private readonly IFotoStorage _fotos;
    private readonly IClock _relogio;

    public DefinirFotoProduto(IProdutoRepository produtos, IFotoStorage fotos, IClock relogio)
    {
        _produtos = produtos;
        _fotos = fotos;
        _relogio = relogio;
    }

    public Resultado<Produto> Executar(int produtoId, string caminhoOrigem)
    {
        var produto = _produtos.BuscarPorId(produtoId);
        if (produto is null)
        {
            return Resultado<Produto>.Falha("Produto não encontrado.");
        }

        string novoArquivo;
        try
        {
            novoArquivo = _fotos.Importar(caminhoOrigem);
        }
        catch (FotoInvalidaException ex)
        {
            return Resultado<Produto>.Falha(ex.Message);
        }

        var arquivoAnterior = produto.FotoArquivo;
        produto.FotoArquivo = novoArquivo;
        produto.AtualizadoEm = _relogio.UtcNow;

        try
        {
            _produtos.Salvar(produto);
        }
        catch
        {
            _fotos.Excluir(novoArquivo);
            throw;
        }

        if (!string.IsNullOrEmpty(arquivoAnterior))
        {
            _fotos.Excluir(arquivoAnterior);
        }

        return Resultado<Produto>.Ok(produto);
    }
}
