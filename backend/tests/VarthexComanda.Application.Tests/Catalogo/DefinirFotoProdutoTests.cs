using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Catalogo;

public class DefinirFotoProdutoTests
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
    public void Executar_ProdutoInexistente_FalhaSemImportarNada()
    {
        var (produtos, _) = CriarProduto();
        var fotos = new FakeFotoStorage();
        var caso = new DefinirFotoProduto(produtos, fotos, new FakeClock());

        var resultado = caso.Executar(999, "C:\\foto.jpg");

        Assert.False(resultado.Sucesso);
        Assert.Contains("Produto não encontrado.", resultado.Erros);
        Assert.Empty(fotos.Importados);
    }

    [Fact]
    public void Executar_FotoValida_GravaONomeDoArquivoNoProduto()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage();
        var caso = new DefinirFotoProduto(produtos, fotos, new FakeClock());

        var resultado = caso.Executar(produto.Id, "C:\\foto.jpg");

        Assert.True(resultado.Sucesso);
        Assert.Equal("foto1.jpg", resultado.Valor!.FotoArquivo);
        Assert.Equal("foto1.jpg", produtos.BuscarPorId(produto.Id)!.FotoArquivo);
        Assert.Empty(fotos.Excluidos);
    }

    [Fact]
    public void Executar_ProdutoJaTinhaFoto_ExcluiOArquivoAntigo()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage();
        var caso = new DefinirFotoProduto(produtos, fotos, new FakeClock());
        caso.Executar(produto.Id, "C:\\primeira.jpg");

        var resultado = caso.Executar(produto.Id, "C:\\segunda.jpg");

        Assert.True(resultado.Sucesso);
        Assert.Equal("foto2.jpg", produtos.BuscarPorId(produto.Id)!.FotoArquivo);
        Assert.Equal(new[] { "foto1.jpg" }, fotos.Excluidos);
    }

    [Fact]
    public void Executar_StorageRejeitaAFoto_FalhaComMensagemESemAlterarOProduto()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage { MensagemRejeicao = "Formato não suportado. Use JPG, PNG ou BMP." };
        var caso = new DefinirFotoProduto(produtos, fotos, new FakeClock());

        var resultado = caso.Executar(produto.Id, "C:\\documento.pdf");

        Assert.False(resultado.Sucesso);
        Assert.Contains("Formato não suportado. Use JPG, PNG ou BMP.", resultado.Erros);
        Assert.Null(produtos.BuscarPorId(produto.Id)!.FotoArquivo);
    }

    [Fact]
    public void Executar_FalhaAoSalvar_ExcluiOArquivoNovoEPropagaAExcecao()
    {
        var (produtos, produto) = CriarProduto();
        var fotos = new FakeFotoStorage();
        var caso = new DefinirFotoProduto(new RepositorioQueFalhaAoSalvar(produtos), fotos, new FakeClock());

        Assert.Throws<InvalidOperationException>(() => caso.Executar(produto.Id, "C:\\foto.jpg"));

        Assert.Equal(new[] { "foto1.jpg" }, fotos.Excluidos);
    }

    private sealed class RepositorioQueFalhaAoSalvar : IProdutoRepository
    {
        private readonly FakeProdutoRepository _interno;

        public RepositorioQueFalhaAoSalvar(FakeProdutoRepository interno) => _interno = interno;

        public Produto? BuscarPorId(int id) => _interno.BuscarPorId(id);

        public IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto) => _interno.Pesquisar(categoriaId, texto);

        public Produto Salvar(Produto produto) => throw new InvalidOperationException("falha simulada");
    }
}
