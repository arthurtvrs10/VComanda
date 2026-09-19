using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Desktop.Catalogo;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Catalogo;

public class ProdutosViewModelTests
{
    private static ProdutosViewModel CriarViewModel(FakeCategoriaRepository categorias, FakeProdutoRepository produtos, FakeFotoStorage? fotos = null)
    {
        var relogio = new FakeClock();
        fotos ??= new FakeFotoStorage();
        return new ProdutosViewModel(
            new ListarCategoriasAtivas(categorias),
            new CadastrarCategoria(categorias, relogio),
            new PesquisarProdutos(produtos),
            new CadastrarProduto(produtos, categorias, relogio),
            new AlterarProduto(produtos, categorias, relogio),
            new DesativarProduto(produtos, relogio),
            new DefinirFotoProduto(produtos, fotos, relogio),
            new RemoverFotoProduto(produtos, fotos, relogio));
    }

    [Fact]
    public void PerderSelecao_LimpaFormulario_NaoDuplicaAoSalvarDeNovo()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var viewModel = CriarViewModel(categorias, produtos);
        viewModel.CategoriaProduto = categoria;
        viewModel.NomeProduto = "Refrigerante";
        viewModel.PrecoProdutoReais = "5,00";
        viewModel.SalvarCommand.Execute(null);

        viewModel.ProdutoSelecionado = viewModel.Produtos[0];
        viewModel.ProdutoSelecionado = null;
        viewModel.SalvarCommand.Execute(null);

        Assert.Single(produtos.Pesquisar(null, null));
    }

    [Fact]
    public void PrecoComPonto_NaoEhInterpretadoComoMilhar()
    {
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var viewModel = CriarViewModel(categorias, produtos);
        viewModel.CategoriaProduto = categoria;
        viewModel.NomeProduto = "Refrigerante";
        viewModel.PrecoProdutoReais = "10.50";

        viewModel.SalvarCommand.Execute(null);

        Assert.Empty(produtos.Pesquisar(null, null));
        Assert.Equal("Informe um preço válido.", viewModel.Mensagem);
    }

    private static (ProdutosViewModel ViewModel, FakeFotoStorage Fotos, Produto Produto) CriarComProdutoSelecionado()
    {
        var categorias = new FakeCategoriaRepository();
        var produtos = new FakeProdutoRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var criado = new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500).Valor!;
        var fotos = new FakeFotoStorage();
        var viewModel = CriarViewModel(categorias, produtos, fotos);
        var selecionado = viewModel.Produtos.Single(p => p.Id == criado.Id);
        viewModel.ProdutoSelecionado = selecionado;
        return (viewModel, fotos, selecionado);
    }

    [Fact]
    public void DefinirFoto_SemProdutoSelecionado_MostraMensagem()
    {
        var viewModel = CriarViewModel(new FakeCategoriaRepository(), new FakeProdutoRepository());

        viewModel.DefinirFoto("C:\\foto.jpg");

        Assert.Equal("Selecione um produto para definir a foto.", viewModel.Mensagem);
        Assert.Null(viewModel.FotoArquivoAtual);
    }

    [Fact]
    public void DefinirFoto_ProdutoSelecionado_AtualizaAFotoAtual()
    {
        var (viewModel, _, produto) = CriarComProdutoSelecionado();

        viewModel.DefinirFoto("C:\\foto.jpg");

        Assert.Equal("foto1.jpg", viewModel.FotoArquivoAtual);
        Assert.Equal("foto1.jpg", produto.FotoArquivo);
        Assert.Equal(string.Empty, viewModel.Mensagem);
    }

    [Fact]
    public void DefinirFoto_StorageRejeita_MostraAMensagemDoStorage()
    {
        var (viewModel, fotos, _) = CriarComProdutoSelecionado();
        fotos.MensagemRejeicao = "Formato não suportado. Use JPG, PNG ou BMP.";

        viewModel.DefinirFoto("C:\\documento.pdf");

        Assert.Equal("Formato não suportado. Use JPG, PNG ou BMP.", viewModel.Mensagem);
        Assert.Null(viewModel.FotoArquivoAtual);
    }

    [Fact]
    public void RemoverFoto_ComFoto_LimpaAFotoAtual()
    {
        var (viewModel, _, produto) = CriarComProdutoSelecionado();
        viewModel.DefinirFoto("C:\\foto.jpg");

        viewModel.RemoverFotoCommand.Execute(null);

        Assert.Null(viewModel.FotoArquivoAtual);
        Assert.Null(produto.FotoArquivo);
    }

    [Fact]
    public void RemoverFotoCommand_SemFoto_FicaDesabilitado()
    {
        var (viewModel, _, _) = CriarComProdutoSelecionado();

        Assert.False(viewModel.RemoverFotoCommand.CanExecute(null));
    }

    [Fact]
    public void SelecionarProduto_CarregaAFotoDoProdutoEPerdeSelecaoLimpa()
    {
        var (viewModel, _, _) = CriarComProdutoSelecionado();
        viewModel.DefinirFoto("C:\\foto.jpg");
        Assert.True(viewModel.RemoverFotoCommand.CanExecute(null));
        Assert.True(viewModel.TemProdutoSelecionado);

        viewModel.ProdutoSelecionado = null;

        Assert.Null(viewModel.FotoArquivoAtual);
        Assert.False(viewModel.TemProdutoSelecionado);
    }
}
