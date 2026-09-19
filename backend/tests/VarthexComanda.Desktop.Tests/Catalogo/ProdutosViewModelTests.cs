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
    public void DefinirFoto_SemProdutoSelecionado_GuardaCaminhoPendente()
    {
        var viewModel = CriarViewModel(new FakeCategoriaRepository(), new FakeProdutoRepository());

        viewModel.DefinirFoto("C:\\foto.jpg");

        Assert.Equal("C:\\foto.jpg", viewModel.FotoPendenteCaminho);
        Assert.Equal(string.Empty, viewModel.Mensagem);
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

    private static (ProdutosViewModel ViewModel, FakeProdutoRepository Produtos, FakeFotoStorage Fotos) CriarComFormularioNovoPreenchido()
    {
        var categorias = new FakeCategoriaRepository();
        new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas");
        var produtos = new FakeProdutoRepository();
        var fotos = new FakeFotoStorage();
        var viewModel = CriarViewModel(categorias, produtos, fotos);
        viewModel.NomeProduto = "Refrigerante";
        viewModel.CategoriaProduto = viewModel.Categorias[0];
        viewModel.PrecoProdutoReais = "5,00";
        return (viewModel, produtos, fotos);
    }

    [Fact]
    public void Cadastrar_ComFotoPendente_GravaAFotoNoProdutoCriadoELimpaOFormulario()
    {
        var (viewModel, produtos, _) = CriarComFormularioNovoPreenchido();
        viewModel.DefinirFoto("C:\\foto.jpg");

        viewModel.SalvarCommand.Execute(null);

        var criado = Assert.Single(produtos.Pesquisar(null, null));
        Assert.Equal("foto1.jpg", criado.FotoArquivo);
        Assert.Null(viewModel.ProdutoSelecionado);
        Assert.Null(viewModel.FotoPendenteCaminho);
    }

    [Fact]
    public void Cadastrar_FotoRejeitada_ProdutoFicaCadastradoESelecionadoComMensagem()
    {
        var (viewModel, produtos, fotos) = CriarComFormularioNovoPreenchido();
        fotos.MensagemRejeicao = "Formato não suportado. Use JPG, PNG ou BMP.";
        viewModel.DefinirFoto("C:\\documento.pdf");

        viewModel.SalvarCommand.Execute(null);

        var criado = Assert.Single(produtos.Pesquisar(null, null));
        Assert.NotNull(viewModel.ProdutoSelecionado);
        Assert.Equal(criado.Id, viewModel.ProdutoSelecionado!.Id);
        Assert.Equal("EDITAR PRODUTO", viewModel.TituloFormulario);
        Assert.Contains("Produto cadastrado, mas a foto não foi adicionada", viewModel.Mensagem);
        Assert.Contains("Formato não suportado. Use JPG, PNG ou BMP.", viewModel.Mensagem);
    }

    [Fact]
    public void RemoverFoto_ComFotoPendente_LimpaOPendente()
    {
        var viewModel = CriarViewModel(new FakeCategoriaRepository(), new FakeProdutoRepository());
        viewModel.DefinirFoto("C:\\foto.jpg");

        viewModel.RemoverFotoCommand.Execute(null);

        Assert.Null(viewModel.FotoPendenteCaminho);
    }

    [Fact]
    public void RemoverFotoCommand_ComFotoPendente_FicaHabilitado()
    {
        var viewModel = CriarViewModel(new FakeCategoriaRepository(), new FakeProdutoRepository());
        Assert.False(viewModel.RemoverFotoCommand.CanExecute(null));

        viewModel.DefinirFoto("C:\\foto.jpg");

        Assert.True(viewModel.RemoverFotoCommand.CanExecute(null));
    }

    [Fact]
    public void SelecionarProduto_DescartaFotoPendente()
    {
        var (viewModel, _, produto) = CriarComProdutoSelecionado();
        viewModel.ProdutoSelecionado = null;
        viewModel.DefinirFoto("C:\\foto.jpg");
        Assert.NotNull(viewModel.FotoPendenteCaminho);

        viewModel.ProdutoSelecionado = produto;

        Assert.Null(viewModel.FotoPendenteCaminho);
    }

    [Fact]
    public void TituloFormulario_AcompanhaOModo()
    {
        var (viewModel, _, produto) = CriarComProdutoSelecionado();
        viewModel.ProdutoSelecionado = null;
        Assert.Equal("NOVO PRODUTO", viewModel.TituloFormulario);

        viewModel.ProdutoSelecionado = produto;

        Assert.Equal("EDITAR PRODUTO", viewModel.TituloFormulario);
    }
}
