using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class RemoverFotoProdutoTests
{
    private static (FakeProdutoRepository Produtos, Produto Produto) CriarProduto()
    {
        var categorias = new FakeCategoriaRepository();
        var produtos = new FakeProdutoRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var produto = new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500).Valor!;
        return (produtos, produto);
    }

    [Fact]
    public void Executar_ProdutoInexistente_Falha()
    {
        var (produtos, _) = CriarProduto();
        var caso = new RemoverFotoProduto(produtos, new FakeFotoStorage(), new FakeClock());

        var resultado = caso.Executar(999);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Produto não encontrado.", resultado.Erros);
    }

    [Fact]
    public void Executar_ProdutoSemFoto_SucessoSemExcluirNada()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage();
        var caso = new RemoverFotoProduto(produtos, fotos, new FakeClock());

        var resultado = caso.Executar(produto.Id);

        Assert.True(resultado.Sucesso);
        Assert.Empty(fotos.Excluidos);
    }

    [Fact]
    public void Executar_ProdutoComFoto_LimpaOCampoEExcluiOArquivo()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage();
        new DefinirFotoProduto(produtos, fotos, new FakeClock()).Executar(produto.Id, "C:\\foto.jpg");
        var caso = new RemoverFotoProduto(produtos, fotos, new FakeClock());

        var resultado = caso.Executar(produto.Id);

        Assert.True(resultado.Sucesso);
        Assert.Null(produtos.BuscarPorId(produto.Id)!.FotoArquivo);
        Assert.Equal(new[] { "foto1.jpg" }, fotos.Excluidos);
    }

    [Fact]
    public void Executar_FalhaAoSalvar_NaoExcluiOArquivoEPropagaAExcecao()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage();
        new DefinirFotoProduto(produtos, fotos, new FakeClock()).Executar(produto.Id, "C:\\foto.jpg");
        var caso = new RemoverFotoProduto(new RepositorioQueFalhaAoSalvar(produtos), fotos, new FakeClock());

        Assert.Throws<InvalidOperationException>(() => caso.Executar(produto.Id));

        Assert.Empty(fotos.Excluidos);
    }
}
